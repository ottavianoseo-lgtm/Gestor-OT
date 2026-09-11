using GestorOT.Infrastructure.Services;
using GestorOT.Shared;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// OT-34, coloreado de lotes por cultivo.
///
/// Tres cosas se rompen sin hacer ruido y por eso estan medidas: que agregar el cultivo cambie
/// el payload de los que piden el GeoJSON sin campaña, que con doble cultivo se elija cualquiera
/// de los dos, y que dos cultivos distintos compartan color —que es lo que hace ilegible el mapa.
/// </summary>
public class CropSymbologyTests
{
    private static readonly Guid Trigo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Soja = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static CampaignCrop Rotacion(Guid id, string nombre, string desde, string hasta) =>
        new(id, nombre, DateOnly.Parse(desde), DateOnly.Parse(hasta));

    // ---------------------------------------------------------------- payload

    [Fact]
    public void SinCampania_ElFeatureNoCambia()
    {
        // El criterio de aceptacion literal: sin campaignId la respuesta es la de antes.
        var feature = LotQueryService.BuildLotFeature(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Lote 1", "Active",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "La Celina", 76.5, crop: null, geometry: null);

        Assert.Equal(
            new[] { "id", "name", "status", "fieldId", "fieldName", "area" },
            feature.Properties!.Keys);

        Assert.Equal("Lote 1", feature.Properties["name"]);
        Assert.Equal(76.5, feature.Properties["area"]);
    }

    [Fact]
    public void ConCultivo_SeAgreganClavesSinTocarLasDeAntes()
    {
        var feature = LotQueryService.BuildLotFeature(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Lote 1", "Active",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "La Celina", 76.5,
            new LotCropInfo(Soja, "Soja", "#27AE60"),
            geometry: null);

        Assert.Equal("Soja", feature.Properties!["cropName"]);
        Assert.Equal(Soja.ToString(), feature.Properties["cropId"]);
        Assert.Equal("#27AE60", feature.Properties["cropColor"]);

        // Las originales siguen intactas.
        Assert.Equal("Lote 1", feature.Properties["name"]);
        Assert.Equal(76.5, feature.Properties["area"]);
    }

    // ---------------------------------------------------------------- que cultivo

    [Fact]
    public void ConDobleCultivo_MandaElQueEstaSembradoHoy()
    {
        // El caso que motiva tener una regla: trigo y despues soja en la misma campaña.
        // FirstOrDefault() sin ordenar devolveria cualquiera de los dos.
        var rotaciones = new[]
        {
            Rotacion(Soja, "Soja", "2026-12-01", "2027-05-30"),
            Rotacion(Trigo, "Trigo", "2026-06-01", "2026-11-30")
        };

        var elegido = CropSelection.ForDate(rotaciones, DateOnly.Parse("2026-09-11"));

        Assert.Equal("Trigo", elegido!.Value.CropName);
    }

    [Fact]
    public void ElOrdenDeEntradaNoCambiaElResultado()
    {
        // Si el orden de la consulta cambiara el cultivo, el color cambiaria solo.
        var a = Rotacion(Trigo, "Trigo", "2026-06-01", "2026-11-30");
        var b = Rotacion(Soja, "Soja", "2026-12-01", "2027-05-30");
        var hoy = DateOnly.Parse("2027-01-15");

        Assert.Equal(
            CropSelection.ForDate(new[] { a, b }, hoy)!.Value.CropName,
            CropSelection.ForDate(new[] { b, a }, hoy)!.Value.CropName);
    }

    [Fact]
    public void CampaniaTerminada_MuestraElUltimoCultivoSembrado()
    {
        // Preferible a dejar el lote en gris como si nunca hubiera tenido nada.
        var rotaciones = new[]
        {
            Rotacion(Trigo, "Trigo", "2025-06-01", "2025-11-30"),
            Rotacion(Soja, "Soja", "2025-12-01", "2026-05-30")
        };

        Assert.Equal("Soja", CropSelection.ForDate(rotaciones, DateOnly.Parse("2026-09-11"))!.Value.CropName);
    }

    [Fact]
    public void CampaniaQueNoArranco_MuestraLoPrimeroPlanificado()
    {
        var rotaciones = new[]
        {
            Rotacion(Soja, "Soja", "2027-12-01", "2028-05-30"),
            Rotacion(Trigo, "Trigo", "2027-06-01", "2027-11-30")
        };

        Assert.Equal("Trigo", CropSelection.ForDate(rotaciones, DateOnly.Parse("2026-09-11"))!.Value.CropName);
    }

    [Fact]
    public void SinRotaciones_NoHayCultivo()
    {
        Assert.Null(CropSelection.ForDate(Array.Empty<CampaignCrop>(), DateOnly.Parse("2026-09-11")));
    }

    // ---------------------------------------------------------------- paleta

    [Fact]
    public void CultivosDistintosDelCatalogo_NuncaCompartenColor()
    {
        // Es la razon de asignar por catalogo y no por hash: con hash, Trigo y Maiz caian en
        // el mismo tono y el mapa dejaba de distinguirlos.
        var colores = Enumerable.Range(0, CropPalette.Count)
            .Select(i => CropPalette.ColorFor("Cultivo " + i, i))
            .ToList();

        Assert.Equal(colores.Count, colores.Distinct().Count());
    }

    [Fact]
    public void ElColorNoDependeDelNombreCuandoHayCatalogo()
    {
        // Renombrar un cultivo en el ERP no le cambia el color: manda la posicion.
        Assert.Equal(CropPalette.ColorFor("Soja", 3), CropPalette.ColorFor("Soja de segunda", 3));
    }

    [Fact]
    public void MasCultivosQueColores_VuelveAEmpezar()
    {
        // Documentado a proposito: pasando la paleta se repite. Con doce colores y los
        // cultivos de un establecimiento real no se llega, pero que quede medido.
        Assert.Equal(CropPalette.ColorFor("A", 0), CropPalette.ColorFor("B", CropPalette.Count));
    }

    [Fact]
    public void SinCatalogo_ElColorSigueSiendoEstable()
    {
        // Fallback para una actividad que no esta en el catalogo del tenant. Puede colisionar,
        // pero no puede cambiar entre reinicios: con string.GetHashCode() cambiaria.
        Assert.Equal("#795548", CropPalette.ColorFor("Trigo"));
        Assert.Equal("#9BCB3B", CropPalette.ColorFor("Soja"));
    }

    [Theory]
    [InlineData("Soja", "soja")]
    [InlineData("Soja", " Soja ")]
    [InlineData("Soja", "SOJA")]
    public void SinCatalogo_ElNombreSeNormaliza(string a, string b)
    {
        Assert.Equal(CropPalette.ColorFor(a), CropPalette.ColorFor(b));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinCultivo_VaElGrisReservado(string? nombre)
    {
        Assert.Equal(CropPalette.NoCrop, CropPalette.ColorFor(nombre, 0));
    }

    [Fact]
    public void LaPaletaNoUsaElGrisReservado()
    {
        // El gris significa "sin cultivo": si un cultivo real cayera ahi, la leyenda mentiria.
        var todos = Enumerable.Range(0, CropPalette.Count).Select(i => CropPalette.ColorFor("X", i));

        Assert.All(todos, c => Assert.NotEqual(CropPalette.NoCrop, c));
    }
}
