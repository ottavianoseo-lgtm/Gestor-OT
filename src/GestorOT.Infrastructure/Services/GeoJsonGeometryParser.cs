using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;

namespace GestorOT.Infrastructure.Services;

/// <summary>
/// Convierte la columna GIS de la planilla (GeoJSON, y por si acaso WKT) en una geometría 4326
/// lista para persistir en el lote.
///
/// No usa NetTopologySuite.IO.GeoJson para no sumar una dependencia que no estaba en el proyecto:
/// el subconjunto que llega —Polygon/MultiPolygon de la planilla— es chico y se arma a mano.
///
/// Además de leer, normaliza: los exports que salen de shapefile suelen escribir cada anillo
/// como un polígono suelto del MultiPolygon, así que los agujeros llegan como shells adentro de
/// otro shell y OGC los marca inválidos. Rechazar esas filas dejaba afuera la mayor parte del GIS
/// de un archivo perfectamente utilizable, así que se re-anidan por contención y, si aún queda
/// algo roto, se pasa por <see cref="GeometryFixer"/>.
/// </summary>
public static class GeoJsonGeometryParser
{
    /// <summary>
    /// Devuelve false solo si venía algo que no se pudo interpretar. Un texto vacío es válido:
    /// el GIS es opcional y hay lotes sin relevar.
    /// </summary>
    public static bool TryParse(string? raw, out Geometry? geometry, out string? error)
        => TryParse(raw, out geometry, out error, out _);

    /// <param name="reparada">
    /// true cuando lo que venía en la planilla era topológicamente inválido y hubo que
    /// normalizarlo. Sirve para avisar en la vista previa: la geometría entra, pero conviene
    /// que alguien la mire.
    /// </param>
    public static bool TryParse(string? raw, out Geometry? geometry, out string? error, out bool reparada)
    {
        geometry = null;
        error = null;
        reparada = false;

        if (string.IsNullOrWhiteSpace(raw)) return true;

        var text = raw.Trim();
        var factory = new GeometryFactory(new PrecisionModel(), 4326);

        Geometry crudo;
        List<LinearRing>? anillos = null;

        // WKT: por si pegan la geometría ya serializada en vez del JSON.
        if (text.StartsWith("POLYGON", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("MULTIPOLYGON", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                crudo = new WKTReader(factory.GeometryServices).Read(text);
                crudo.SRID = 4326;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
        else
        {
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

                if (string.Equals(type, "Polygon", StringComparison.OrdinalIgnoreCase))
                {
                    anillos = LeerAnillos(coords);
                    crudo = factory.CreatePolygon(anillos[0], anillos.Skip(1).ToArray());
                }
                else if (string.Equals(type, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
                {
                    anillos = new List<LinearRing>();
                    var poligonos = new List<Polygon>();
                    foreach (var polyEl in coords.EnumerateArray())
                    {
                        var delPoligono = LeerAnillos(polyEl);
                        anillos.AddRange(delPoligono);
                        poligonos.Add(factory.CreatePolygon(delPoligono[0], delPoligono.Skip(1).ToArray()));
                    }
                    crudo = factory.CreateMultiPolygon(poligonos.ToArray());
                }
                else
                {
                    error = $"el tipo '{type}' no es un polígono";
                    return false;
                }
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

        if (crudo.IsEmpty)
        {
            error = "la geometría está vacía";
            return false;
        }

        if (EsValida(crudo))
        {
            geometry = crudo;
            return true;
        }

        // 1) Re-anidar por contención: recupera los agujeros que el export mandó como shells.
        if (anillos is { Count: > 1 })
        {
            var reAnidada = ReAnidarPorContencion(anillos, factory);
            if (reAnidada != null && EsValida(reAnidada))
            {
                geometry = reAnidada;
                reparada = true;
                return true;
            }
            crudo = reAnidada ?? crudo;
        }

        // 2) Último recurso: el fixer de NTS, que conserva el tipo poligonal y descarta lo degenerado.
        try
        {
            var arreglada = GeometryFixer.Fix(crudo);
            if (arreglada != null && !arreglada.IsEmpty && EsPoligonal(arreglada) && EsValida(arreglada))
            {
                arreglada.SRID = 4326;
                geometry = arreglada;
                reparada = true;
                return true;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        error = "el polígono es topológicamente inválido y no se pudo corregir automáticamente";
        return false;
    }

    /// <summary>
    /// Arma la geometría a partir de todos los anillos sueltos, decidiendo shell o agujero por
    /// profundidad de anidamiento: un anillo contenido en una cantidad par de anillos es shell,
    /// y en cantidad impar es agujero del anillo que lo contiene más de cerca. Es la misma regla
    /// que usa un shapefile para interpretar sus anillos.
    /// </summary>
    private static Geometry? ReAnidarPorContencion(List<LinearRing> anillos, GeometryFactory factory)
    {
        var n = anillos.Count;
        var comoPoligono = new Polygon[n];
        for (var i = 0; i < n; i++)
            comoPoligono[i] = factory.CreatePolygon(anillos[i]);

        var profundidad = new int[n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (i != j && ContieneEstrictamente(comoPoligono[j], comoPoligono[i]))
                    profundidad[i]++;
            }
        }

        var padre = new int[n];
        Array.Fill(padre, -1);
        for (var i = 0; i < n; i++)
        {
            if (profundidad[i] % 2 == 0) continue;

            for (var j = 0; j < n; j++)
            {
                if (i != j && profundidad[j] == profundidad[i] - 1
                    && ContieneEstrictamente(comoPoligono[j], comoPoligono[i]))
                {
                    padre[i] = j;
                    break;
                }
            }

            // Anillo impar sin padre: la contención no cerró (anillos que se solapan en vez de
            // anidarse). Se lo trata como shell y que decida el fixer.
            if (padre[i] < 0) profundidad[i] = 0;
        }

        var resultado = new List<Polygon>();
        for (var i = 0; i < n; i++)
        {
            if (profundidad[i] % 2 != 0) continue;

            var agujeros = new List<LinearRing>();
            for (var k = 0; k < n; k++)
            {
                if (padre[k] == i) agujeros.Add(anillos[k]);
            }
            resultado.Add(factory.CreatePolygon(anillos[i], agujeros.ToArray()));
        }

        if (resultado.Count == 0) return null;

        return resultado.Count == 1
            ? resultado[0]
            : factory.CreateMultiPolygon(resultado.ToArray());
    }

    /// <summary>
    /// Contención en un solo sentido. Sin el "y no al revés", dos anillos casi idénticos se
    /// declaran padres mutuos y todos quedan en profundidad impar, sin ningún shell.
    /// </summary>
    private static bool ContieneEstrictamente(Polygon contenedor, Polygon candidato)
    {
        try
        {
            if (!contenedor.EnvelopeInternal.Contains(candidato.EnvelopeInternal)) return false;
            return contenedor.Covers(candidato) && !candidato.Covers(contenedor);
        }
        catch
        {
            // Los anillos auto-intersectados rompen el predicado; para el anidado valen como
            // "no contiene" y después los levanta el fixer.
            return false;
        }
    }

    private static List<LinearRing> LeerAnillos(JsonElement polygon)
    {
        var anillos = new List<LinearRing>();
        for (var i = 0; i < polygon.GetArrayLength(); i++)
            anillos.Add(LeerAnillo(polygon[i]));
        return anillos;
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

    private static bool EsPoligonal(Geometry geometry)
        => geometry is Polygon or MultiPolygon;

    private static bool EsValida(Geometry? geometry)
    {
        if (geometry == null || geometry.IsEmpty) return false;

        try
        {
            return geometry.IsValid;
        }
        catch
        {
            return false;
        }
    }
}
