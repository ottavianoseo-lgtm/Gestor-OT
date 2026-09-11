using System.IO.Compression;
using System.Text;
using GestorOT.Infrastructure.Services;
using GestorOT.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// OT-46. El importador elegía cada componente del shapefile (.shp, .dbf, .shx, .prj, .cpg) por
/// separado, quedándose con el primero de cada extensión. Con un zip de varios shapefiles —que es
/// como llegan los archivos del cliente— eso producía un lote con la geometría de un polígono y
/// los atributos de otro, sin ningún aviso.
///
/// Ahora las entradas se agrupan por nombre base antes de elegir, y si hay más de un shapefile
/// se falla en vez de adivinar.
///
/// Estos tests cubren el armado del zip y la elección de componentes, que es donde estaba el
/// defecto. No llegan a la reproyección, que necesita PostGIS.
/// </summary>
public class ShapefileZipGroupingTests
{
    private static readonly GeometryFactory Factory = new();

    private const string PrjWgs84 =
        "GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]]," +
        "PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]]";

    /// <summary>Escribe un shapefile con un cuadrado y una columna Lote, y devuelve sus archivos.</summary>
    private static Dictionary<string, byte[]> BuildShapefile(string nombreLote, double x, double y)
    {
        var dir = Path.Combine(Path.GetTempPath(), "shptest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var polygon = Factory.CreatePolygon(new[]
            {
                new Coordinate(x, y), new Coordinate(x + 0.01, y),
                new Coordinate(x + 0.01, y + 0.01), new Coordinate(x, y + 0.01),
                new Coordinate(x, y)
            });

            var attrs = new AttributesTable();
            attrs.Add("Lote", nombreLote);

            var features = new List<IFeature> { new Feature(polygon, attrs) };

            var basePath = Path.Combine(dir, "shape");
            var writer = new ShapefileDataWriter(basePath, Factory, Encoding.Latin1);
            writer.Header = ShapefileDataWriter.GetHeader(features[0], features.Count, Encoding.Latin1);
            writer.Write(features);
            File.WriteAllText(basePath + ".prj", PrjWgs84, new UTF8Encoding(false));

            var result = new Dictionary<string, byte[]>();
            foreach (var f in Directory.GetFiles(dir))
            {
                result[Path.GetExtension(f).ToLowerInvariant()] = File.ReadAllBytes(f);
            }

            return result;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static MemoryStream BuildZip(params (string RutaBase, Dictionary<string, byte[]> Archivos)[] shapefiles)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (rutaBase, archivos) in shapefiles)
            {
                foreach (var (ext, bytes) in archivos)
                {
                    var entry = zip.CreateEntry(rutaBase + ext);
                    using var s = entry.Open();
                    s.Write(bytes, 0, bytes.Length);
                }
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static ShapefileImportService CreateService() =>
        new(context: null!, NullLogger<ShapefileImportService>.Instance);

    [Fact]
    public async Task UnZipConVariosShapefiles_Falla_YListaLosQueEncontro()
    {
        // La estructura del zip real de La Celina: varios shapefiles en la misma carpeta.
        using var zip = BuildZip(
            ("LCl_1", BuildShapefile("1", -60.0, -34.0)),
            ("LCl_2", BuildShapefile("2", -60.1, -34.0)),
            ("LCl_3", BuildShapefile("3", -60.2, -34.0)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ReadZipAsync(zip));

        Assert.Contains("3 shapefiles", ex.Message);
        Assert.Contains("LCl_1", ex.Message);
        Assert.Contains("LCl_2", ex.Message);
        Assert.Contains("LCl_3", ex.Message);
    }

    [Fact]
    public async Task DosShapefilesHomonimosEnCarpetasDistintas_CuentanComoDos()
    {
        // El agrupamiento usa la ruta completa: si usara solo el nombre, estos dos colisionarían
        // y se mezclarían entre sí.
        using var zip = BuildZip(
            ("campoA/lotes", BuildShapefile("A", -60.0, -34.0)),
            ("campoB/lotes", BuildShapefile("B", -61.0, -35.0)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ReadZipAsync(zip));

        Assert.Contains("2 shapefiles", ex.Message);
    }

    [Fact]
    public async Task ShpSinSuDbf_DiceQueArchivoFalta()
    {
        // Antes tomaba el .dbf de cualquier otro shapefile; ahora exige el del mismo nombre base.
        var archivos = BuildShapefile("1", -60.0, -34.0);
        archivos.Remove(".dbf");

        using var zip = BuildZip(("LCl_1", archivos));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ReadZipAsync(zip));

        Assert.Contains("LCl_1.dbf", ex.Message);
    }

    [Fact]
    public async Task ZipSinNingunShp_LoDiceClaro()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var s = zip.CreateEntry("planimetria.pdf").Open();
            s.Write("no soy un shapefile"u8);
        }
        ms.Position = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ReadZipAsync(ms));

        Assert.Contains(".shp", ex.Message);
    }

    [Fact]
    public async Task LosArchivosBasuraDeMacOs_NoCuentanComoShapefile()
    {
        // __MACOSX/._shape.shp matchea la extensión pero no es un shapefile: si contara, un zip
        // hecho en Mac con un solo shapefile se rechazaría por "varios".
        var archivos = BuildShapefile("1", -60.0, -34.0);

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (ext, bytes) in archivos)
            {
                using var s = zip.CreateEntry("LCl_1" + ext).Open();
                s.Write(bytes, 0, bytes.Length);
            }

            using var basura = zip.CreateEntry("__MACOSX/._LCl_1.shp").Open();
            basura.Write("basura"u8);
        }
        ms.Position = 0;

        // Llega hasta la normalización, que necesita base de datos: el punto es que NO se cayó
        // antes por creer que había dos shapefiles.
        var ex = await Record.ExceptionAsync(() => CreateService().ReadZipAsync(ms));

        Assert.False(ex is InvalidOperationException io && io.Message.Contains("shapefiles"),
            $"No debería contarlos como varios shapefiles. Excepción: {ex?.Message}");
    }
}
