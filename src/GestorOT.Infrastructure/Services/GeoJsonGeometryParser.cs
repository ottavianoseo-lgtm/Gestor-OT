using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GestorOT.Infrastructure.Services;

/// <summary>
/// Convierte la columna GIS de la planilla (GeoJSON, y por si acaso WKT) en una geometría 4326
/// lista para persistir en el lote.
///
/// No usa NetTopologySuite.IO.GeoJson para no sumar una dependencia que no estaba en el proyecto:
/// el subconjunto que llega —Polygon/MultiPolygon de la planilla AMSA— es chico y se arma a mano.
/// </summary>
public static class GeoJsonGeometryParser
{
    /// <summary>
    /// Devuelve false solo si venía algo que no se pudo interpretar. Un texto vacío es válido:
    /// el GIS es opcional y hay lotes sin relevar.
    /// </summary>
    public static bool TryParse(string? raw, out Geometry? geometry, out string? error)
    {
        geometry = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw)) return true;

        var text = raw.Trim();

        // WKT: por si pegan la geometría ya serializada en vez del JSON.
        if (text.StartsWith("POLYGON", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("MULTIPOLYGON", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var wkt = new WKTReader().Read(text);
                wkt.SRID = 4326;
                geometry = wkt;
                return Validar(geometry, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

            // Un Feature envuelve la geometría en "geometry".
            if (string.Equals(type, "Feature", StringComparison.OrdinalIgnoreCase)
                && root.TryGetProperty("geometry", out var geomEl))
            {
                root = geomEl;
                type = root.TryGetProperty("type", out var t2) ? t2.GetString() : null;
            }

            if (!root.TryGetProperty("coordinates", out var coords))
            {
                error = "no tiene coordenadas";
                return false;
            }

            var factory = new GeometryFactory(new PrecisionModel(), 4326);

            if (string.Equals(type, "Polygon", StringComparison.OrdinalIgnoreCase))
            {
                geometry = factory.CreatePolygon(LeerAnillo(coords[0]), LeerAnillosInteriores(coords));
            }
            else if (string.Equals(type, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
            {
                var poligonos = new List<Polygon>();
                foreach (var polyEl in coords.EnumerateArray())
                {
                    poligonos.Add(factory.CreatePolygon(LeerAnillo(polyEl[0]), LeerAnillosInteriores(polyEl)));
                }
                geometry = factory.CreateMultiPolygon(poligonos.ToArray());
            }
            else
            {
                error = $"el tipo '{type}' no es un polígono";
                return false;
            }

            return Validar(geometry, out error);
        }
        catch (JsonException)
        {
            error = "el texto no es un GeoJSON válido";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static LinearRing LeerAnillo(JsonElement ring)
    {
        var puntos = new List<Coordinate>();
        foreach (var punto in ring.EnumerateArray())
        {
            puntos.Add(new Coordinate(punto[0].GetDouble(), punto[1].GetDouble()));
        }

        // GeoJSON exige el anillo cerrado; lo cerramos por las dudas de que venga abierto.
        if (puntos.Count >= 3 && !puntos[0].Equals2D(puntos[^1]))
            puntos.Add(puntos[0]);

        return new LinearRing(puntos.ToArray());
    }

    private static LinearRing[] LeerAnillosInteriores(JsonElement polygon)
    {
        var holes = new List<LinearRing>();
        for (var i = 1; i < polygon.GetArrayLength(); i++)
            holes.Add(LeerAnillo(polygon[i]));
        return holes.ToArray();
    }

    private static bool Validar(Geometry? geometry, out string? error)
    {
        error = null;

        if (geometry == null || geometry.IsEmpty)
        {
            error = "la geometría está vacía";
            return false;
        }

        try
        {
            if (!geometry.IsValid)
            {
                error = "el polígono se auto-intersecta";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        return true;
    }
}
