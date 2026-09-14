using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GestorOT.Shared.Dtos;
using Xunit;

namespace GestorOT.Tests.Regression;

public class GeoJsonMultiImportTests
{
    [Fact]
    public void ZipArchive_WithMultipleGeoJsonFiles_CanBeExtractedAndParsedSeparately()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add first geojson
            var entry1 = archive.CreateEntry("Lote14.geojson");
            using (var writer = new StreamWriter(entry1.Open(), Encoding.UTF8))
            {
                writer.Write("""
                {
                  "type": "FeatureCollection",
                  "features": [
                    {
                      "type": "Feature",
                      "properties": { "name": "Lote 14 Norte", "campo": "Establecimiento Uno" },
                      "geometry": {
                        "type": "Polygon",
                        "coordinates": [[[-63.5, -31.5], [-63.5, -31.6], [-63.6, -31.6], [-63.6, -31.5], [-63.5, -31.5]]]
                      }
                    }
                  ]
                }
                """);
            }

            // Add second geojson
            var entry2 = archive.CreateEntry("Lote15.json");
            using (var writer = new StreamWriter(entry2.Open(), Encoding.UTF8))
            {
                writer.Write("""
                {
                  "type": "Feature",
                  "properties": { "lote": "Lote 15 Sur" },
                  "geometry": {
                    "type": "Polygon",
                    "coordinates": [[[-63.6, -31.5], [-63.6, -31.6], [-63.7, -31.6], [-63.7, -31.5], [-63.6, -31.5]]]
                  }
                }
                """);
            }
        }

        memoryStream.Position = 0;

        using (var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
        {
            var hasShp = readArchive.Entries.Any(e => e.Name.EndsWith(".shp", StringComparison.OrdinalIgnoreCase));
            Assert.False(hasShp);

            var geoEntries = readArchive.Entries
                .Where(e => !e.FullName.StartsWith("__MACOSX", StringComparison.OrdinalIgnoreCase) &&
                            (e.Name.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase) ||
                             e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Assert.Equal(2, geoEntries.Count);
            Assert.Contains(geoEntries, e => e.Name == "Lote14.geojson");
            Assert.Contains(geoEntries, e => e.Name == "Lote15.json");
        }
    }

    [Fact]
    public void ZipArchive_WithShapefile_IsDetectedAsShapefile()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("campo.shp");
            archive.CreateEntry("campo.shx");
            archive.CreateEntry("campo.dbf");
        }

        memoryStream.Position = 0;

        using (var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
        {
            var hasShp = readArchive.Entries.Any(e => e.Name.EndsWith(".shp", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasShp);
        }
    }

    [Fact]
    public void PartialErrors_InZip_AllowsProcessingRemainingEntries()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entryBad = archive.CreateEntry("corrupted.geojson");
            using (var writer = new StreamWriter(entryBad.Open(), Encoding.UTF8))
            {
                writer.Write("INVALID NOT JSON {{{{");
            }

            var entryGood = archive.CreateEntry("valid.geojson");
            using (var writer = new StreamWriter(entryGood.Open(), Encoding.UTF8))
            {
                writer.Write("""
                {
                  "type": "Feature",
                  "properties": { "name": "Lote Valido" },
                  "geometry": {
                    "type": "Polygon",
                    "coordinates": [[[-63.5, -31.5], [-63.5, -31.6], [-63.6, -31.6], [-63.6, -31.5], [-63.5, -31.5]]]
                  }
                }
                """);
            }
        }

        memoryStream.Position = 0;

        var warnings = new List<string>();
        var parsedItems = new List<string>();

        using (var readArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
        {
            foreach (var entry in readArchive.Entries)
            {
                try
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    var text = reader.ReadToEnd();
                    using var doc = JsonDocument.Parse(text);
                    parsedItems.Add(entry.Name);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Error al leer {entry.Name}: {ex.Message}");
                }
            }
        }

        Assert.Single(warnings);
        Assert.Contains("corrupted.geojson", warnings[0]);
        Assert.Single(parsedItems);
        Assert.Equal("valid.geojson", parsedItems[0]);
    }
}
