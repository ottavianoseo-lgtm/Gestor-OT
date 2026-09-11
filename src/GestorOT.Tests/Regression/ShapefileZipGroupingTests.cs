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
/// Las entradas se agrupan por nombre base antes de elegir nada, así cada shapefile conserva sus
/// propios componentes. OT-47 además los importa a todos en vez de quedarse con uno.
///
/// Estos tests cubren el agrupamiento, que es donde estaba el defecto. La reproyección necesita
/// PostGIS y se verifica aparte.
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
    public void UnZipConVariosShapefiles_LosDetectaATodos_ComoBundlesSeparados()
    {
        // La estructura del zip real de La Celina: varios shapefiles en la misma carpeta.
        // OT-46 hacía que esto fallara; OT-47 los soporta, cada uno como un bundle propio.
        using var zip = BuildZip(
            ("LCl_1", BuildShapefile("1", -60.0, -34.0)),
            ("LCl_2", BuildShapefile("2", -60.1, -34.0)),
            ("LCl_3", BuildShapefile("3", -60.2, -34.0)));

        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        var (bundles, anidados) = ShapefileImportService.LocateBundles(archive);

        Assert.Equal(3, bundles.Count);
        Assert.Equal(new[] { "LCl_1", "LCl_2", "LCl_3" }, bundles.Select(b => b.Nombre).ToArray());
        Assert.Empty(anidados);

        // Lo que causaba el bug: cada bundle tiene que traer SUS propios componentes.
        foreach (var b in bundles)
        {
            Assert.True(b.Componentes.ContainsKey(".shp"));
            Assert.True(b.Componentes.ContainsKey(".dbf"));
            foreach (var (_, entry) in b.Componentes)
            {
                Assert.Equal(b.Nombre, Path.GetFileNameWithoutExtension(entry.Name));
            }
        }
    }

    [Fact]
    public void DosShapefilesHomonimosEnCarpetasDistintas_NoSeMezclan()
    {
        // El agrupamiento usa la ruta completa: si usara solo el nombre, estos dos colisionarían
        // y se mezclarían entre sí.
        using var zip = BuildZip(
            ("campoA/lotes", BuildShapefile("A", -60.0, -34.0)),
            ("campoB/lotes", BuildShapefile("B", -61.0, -35.0)));

        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        var (bundles, _) = ShapefileImportService.LocateBundles(archive);

        Assert.Equal(2, bundles.Count);
        Assert.All(bundles, b => Assert.True(b.Componentes.ContainsKey(".shp")));
    }

    [Fact]
    public void ArchivosNoGisYZipAnidado_SeSeparanDeLosShapefiles()
    {
        var archivos = BuildShapefile("1", -60.0, -34.0);

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (ext, bytes) in archivos)
            {
                using var s = zip.CreateEntry("La Celina/LCl_1" + ext).Open();
                s.Write(bytes, 0, bytes.Length);
            }
            using (var s = zip.CreateEntry("La Celina/planimetria.pdf").Open()) s.Write("pdf"u8);
            using (var s = zip.CreateEntry("La Celina/cultivos.qgz").Open()) s.Write("qgz"u8);
            using (var s = zip.CreateEntry("La Celina/Shape_Poligonos_Agricolas.zip").Open()) s.Write("zip"u8);
        }
        ms.Position = 0;

        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var (bundles, anidados) = ShapefileImportService.LocateBundles(archive);

        Assert.Single(bundles);
        Assert.Equal("LCl_1", bundles[0].Nombre);
        Assert.Single(anidados);
        Assert.Contains("Shape_Poligonos_Agricolas.zip", anidados[0]);
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
    public void LosArchivosBasuraDeMacOs_NoCuentanComoShapefile()
    {
        // __MACOSX/._shape.shp matchea la extensión pero no es un shapefile: si contara, un zip
        // hecho en Mac con un solo shapefile aparecería como dos.
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

        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var (bundles, _) = ShapefileImportService.LocateBundles(archive);

        Assert.Single(bundles);
        Assert.Equal("LCl_1", bundles[0].Nombre);
    }
}
