using System.Data;
using System.IO.Compression;
using System.Text;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Streams;
using Npgsql;

namespace GestorOT.Infrastructure.Services;

public class ShapefileImportService : IShapefileImportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ShapefileImportService> _logger;

    /// <summary>
    /// Columnas del .dbf que suelen traer el nombre del lote, en orden de preferencia. Los
    /// exportadores no se ponen de acuerdo, asi que se prueba una lista y si ninguna aparece
    /// se cae a la primera columna de texto.
    /// </summary>
    private static readonly string[] NameColumnCandidates =
    [
        "LOTE", "LOTES", "NOMBRE", "NOMBRE_LOT", "NAME", "FIELD", "FIELDNAME",
        "FIELD_NAME", "POTRERO", "PARCELA", "DESCRIPCIO", "DESCRIPCION", "ID_LOTE"
    ];

    public ShapefileImportService(IApplicationDbContext context, ILogger<ShapefileImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ShapefileImportResultDto> ReadZipAsync(Stream zipStream, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // El zip se pasa a memoria: los stream providers de NTS piden poder reposicionarse, y
        // asi se evitan archivos temporales en disco.
        using var buffer = new MemoryStream();
        await zipStream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);

        byte[]? shp = null, dbf = null, shx = null;
        string? prj = null, cpg = null;
        string? shpEntryName = null;

        foreach (var entry in archive.Entries)
        {
            // Los zips hechos en macOS traen una carpeta __MACOSX con copias basura que
            // matchean las mismas extensiones.
            if (entry.FullName.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.Name.StartsWith("._", StringComparison.Ordinal)) continue;
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var ext = Path.GetExtension(entry.Name).ToLowerInvariant();

            switch (ext)
            {
                case ".shp" when shp is null:
                    shp = await ReadEntryBytesAsync(entry, ct);
                    shpEntryName = Path.GetFileNameWithoutExtension(entry.Name);
                    break;
                case ".dbf" when dbf is null:
                    dbf = await ReadEntryBytesAsync(entry, ct);
                    break;
                case ".shx" when shx is null:
                    shx = await ReadEntryBytesAsync(entry, ct);
                    break;
                case ".prj" when prj is null:
                    prj = Encoding.UTF8.GetString(await ReadEntryBytesAsync(entry, ct)).Trim();
                    break;
                case ".cpg" when cpg is null:
                    cpg = Encoding.UTF8.GetString(await ReadEntryBytesAsync(entry, ct)).Trim();
                    break;
            }
        }

        if (shp is null)
            throw new InvalidOperationException("El .zip no contiene un archivo .shp.");
        if (dbf is null)
            throw new InvalidOperationException("El .zip no contiene el .dbf, que es donde vienen los nombres de los lotes.");

        if (shx is null)
        {
            // El .shx es solo el indice: se puede leer sin el, pero conviene avisar porque
            // suele significar que el zip llego incompleto.
            warnings.Add("Falta el .shx (índice). Se leyó igual, pero revisá que el zip esté completo.");
        }

        if (prj is null)
        {
            warnings.Add("Falta el .prj, así que no se puede saber en qué sistema de coordenadas vienen los polígonos. Se asumió WGS84 (EPSG:4326): si los lotes caen en el lugar equivocado o las hectáreas dan cualquier cosa, es por esto.");
        }

        var (rawFeatures, columns, nameColumn) = ReadFeatures(shp, dbf, shx, cpg, warnings);

        if (rawFeatures.Count == 0)
        {
            warnings.Add("No se encontraron polígonos en el shapefile.");
            return new ShapefileImportResultDto([], DescribeCrs(prj), false, columns, nameColumn, warnings);
        }

        var normalized = await NormalizeAsync(rawFeatures, prj, warnings, ct);

        _logger.LogInformation(
            "Shapefile '{Name}': {Total} geometrias leidas, {Ok} validas, prj={Prj}",
            shpEntryName, rawFeatures.Count, normalized.Count, prj is null ? "(falta)" : "presente");

        return new ShapefileImportResultDto(normalized, DescribeCrs(prj), prj is not null, columns, nameColumn, warnings);
    }

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private (List<(string Name, string Wkt, Dictionary<string, string> Attributes)> Features,
             List<string> Columns,
             string? NameColumn)
        ReadFeatures(byte[] shp, byte[] dbf, byte[]? shx, string? cpg, List<string> warnings)
    {
        var shapeStream = new ByteStreamProvider(StreamTypes.Shape, shp);
        var dataStream = new ByteStreamProvider(StreamTypes.Data, dbf);

        // El overload que toma el registry no acepta Encoding: el encoding se le pasa como
        // stream DataEncoding, que es exactamente lo que es el .cpg (un archivo de texto con
        // el nombre del codepage). Sin .cpg se fuerza Latin1, que es el default historico del
        // dbf; con UTF8 los nombres con enie o acentos salen mal.
        var encodingStream = new ByteStreamProvider(
            StreamTypes.DataEncoding,
            string.IsNullOrWhiteSpace(cpg) ? "ISO-8859-1" : cpg);

        var registry = new ShapefileStreamProviderRegistry(
            shapeStream,
            dataStream,
            shx is not null ? new ByteStreamProvider(StreamTypes.Index, shx) : null,
            validateShapeProvider: true,
            validateDataProvider: true,
            validateIndexProvider: false,
            dataEncodingStream: encodingStream);

        var factory = new GeometryFactory(new PrecisionModel(PrecisionModels.Floating), 4326);
        var writer = new WKTWriter();

        var features = new List<(string, string, Dictionary<string, string>)>();
        var columns = new List<string>();
        string? nameColumn = null;

        using var reader = new ShapefileDataReader(registry, factory);

        for (int i = 0; i < reader.DbaseHeader.NumFields; i++)
        {
            columns.Add(reader.DbaseHeader.Fields[i].Name);
        }

        nameColumn = PickNameColumn(columns);
        if (nameColumn is null && columns.Count > 0)
        {
            warnings.Add($"No se reconoció una columna de nombre entre [{string.Join(", ", columns)}]. Los polígonos vienen sin nombre.");
        }

        int skipped = 0;

        while (reader.Read())
        {
            var geometry = reader.Geometry;
            if (geometry is null || geometry.IsEmpty)
            {
                skipped++;
                continue;
            }

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columns.Count; i++)
            {
                // El campo 0 del DbaseFileHeader corresponde a la columna 1 del reader: la 0
                // es el flag de borrado del formato dbf.
                var value = reader.GetValue(i + 1);
                attributes[columns[i]] = value?.ToString()?.Trim() ?? string.Empty;
            }

            var name = nameColumn is not null && attributes.TryGetValue(nameColumn, out var n)
                ? n
                : string.Empty;

            features.Add((name, writer.Write(geometry), attributes));
        }

        if (skipped > 0)
        {
            warnings.Add($"Se saltearon {skipped} registro(s) sin geometría.");
        }

        return (features, columns, nameColumn);
    }

    private static string? PickNameColumn(List<string> columns)
    {
        foreach (var candidate in NameColumnCandidates)
        {
            var match = columns.FirstOrDefault(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        // Cualquier columna que contenga "lote" o "nombre" antes de rendirse.
        return columns.FirstOrDefault(c =>
            c.Contains("LOTE", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("NOMBRE", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("NAME", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reproyecta a 4326, repara geometrías inválidas y calcula la superficie, todo en una sola
    /// ida a PostGIS. Se le pasa el WKT del .prj tal cual: PROJ lo entiende y evita tener que
    /// mapear el .prj a un código EPSG a mano, que es la parte que se rompe.
    /// </summary>
    private async Task<List<ShapefileFeatureDto>> NormalizeAsync(
        List<(string Name, string Wkt, Dictionary<string, string> Attributes)> features,
        string? prj,
        List<string> warnings,
        CancellationToken ct)
    {
        // ST_MakeValid antes de reproyectar: se trabaja en las unidades originales, donde las
        // tolerancias del formato tienen sentido. ST_CollectionExtract(.., 3) descarta los
        // restos lineales o puntuales que MakeValid puede dejar al arreglar un anillo roto.
        var transformed = prj is null
            ? "ST_SetSRID(ST_MakeValid(ST_GeomFromText(src.wkt)), 4326)"
            : "ST_Transform(ST_MakeValid(ST_GeomFromText(src.wkt)), @prj, 4326)";

        var sql = $"""
            WITH src AS (
                SELECT ord, wkt FROM unnest(@wkts) WITH ORDINALITY AS t(wkt, ord)
            ),
            fixed AS (
                SELECT src.ord,
                       ST_Multi(ST_CollectionExtract({transformed}, 3)) AS g
                FROM src
            )
            SELECT ord,
                   CASE WHEN g IS NULL OR ST_IsEmpty(g) THEN NULL ELSE ST_AsText(g) END AS wkt,
                   CASE WHEN g IS NULL OR ST_IsEmpty(g) THEN 0 ELSE ST_Area(g::geography) / 10000.0 END AS area_ha
            FROM fixed
            ORDER BY ord
            """;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;

            command.Parameters.Add(new NpgsqlParameter("wkts", features.Select(f => f.Wkt).ToArray()));
            if (prj is not null)
            {
                command.Parameters.Add(new NpgsqlParameter("prj", prj));
            }

            var results = new List<ShapefileFeatureDto>();
            int invalid = 0;

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var ord = reader.GetInt64(0);
                var source = features[(int)ord - 1];

                if (await reader.IsDBNullAsync(1, ct))
                {
                    invalid++;
                    continue;
                }

                results.Add(new ShapefileFeatureDto(
                    source.Name,
                    reader.GetString(1),
                    Math.Round(reader.GetDouble(2), 4),
                    source.Attributes));
            }

            if (invalid > 0)
            {
                warnings.Add($"Se descartaron {invalid} polígono(s) que quedaron vacíos al reparar la geometría.");
            }

            return results;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static string? DescribeCrs(string? prj)
    {
        if (string.IsNullOrWhiteSpace(prj)) return null;

        // El nombre del CRS es el primer literal entre comillas del WKT.
        var open = prj.IndexOf('"');
        if (open < 0) return null;
        var close = prj.IndexOf('"', open + 1);
        if (close < 0) return null;

        return prj[(open + 1)..close].Replace('_', ' ');
    }
}
