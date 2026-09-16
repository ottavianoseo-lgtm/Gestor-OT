using System.Text.Json;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// El constructor sin parametros de GeoJsonZipApplyRequestDto se llamaba a si mismo: el
/// `new()` target-typed de `this(new())` resolvia al constructor de copia del record. Al
/// deserializar el body de /api/lots/import/geojson-zip/apply eso era un StackOverflow, que
/// tira abajo el proceso entero (el usuario veia un 502 del proxy, no un error de la API).
/// Si vuelve a pasar, estos tests no "fallan": revientan el host de tests, que es justamente
/// la senal de que el ctor volvio a ser recursivo.
/// </summary>
public class GeoJsonZipApplyDtoTests
{
    [Fact]
    public void ConstructorSinParametros_NoEsRecursivo()
    {
        var dto = new GeoJsonZipApplyRequestDto();

        Assert.NotNull(dto.Items);
        Assert.Empty(dto.Items);
        Assert.Null(dto.CampaignId);
    }

    [Fact]
    public void Deserializar_ElBodyDelApply_DevuelveLosItems()
    {
        var lotId = Guid.NewGuid();
        var json = $$"""
        {"items":[{"lotId":"{{lotId}}","wkt":"POLYGON((0 0,0 1,1 1,1 0,0 0))","gisAreaHa":12.5}],"campaignId":null}
        """;

        var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.GeoJsonZipApplyRequestDto);

        Assert.NotNull(request);
        var item = Assert.Single(request!.Items);
        Assert.Equal(lotId, item.LotId);
        Assert.Equal(12.5m, item.GisAreaHa);
    }
}
