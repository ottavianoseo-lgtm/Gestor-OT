using GestorOT.Infrastructure.Services;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// OT-49, aviso por desvío entre la superficie del polígono y la catastral declarada.
///
/// El criterio del ticket es "avisar en vez de pisar el dato en silencio": un polígono que da
/// muy distinto de lo declarado puede ser una inundación real, pero también puede estar mal
/// trazado, y el sistema no puede distinguirlos. Lo que no puede pasar es que la diferencia se
/// guarde sin que nadie se entere.
/// </summary>
public class CampaignSurfaceWarningTests
{
    [Fact]
    public void DesvioGrande_Avisa()
    {
        // 76 ha declaradas contra 50 medidas: 34% de desvío.
        var aviso = CampaignGeometryService.AvisoPorDesvio("Lote 1", 50, 76);

        Assert.NotNull(aviso);
        Assert.Contains("Lote 1", aviso);
        Assert.Contains("Se guardó igual", aviso);
    }

    [Theory]
    [InlineData(100, 100)]   // exacto
    [InlineData(102, 100)]   // 2%, verde
    [InlineData(108, 100)]   // 8%, ámbar: llamativo pero no alarmante
    [InlineData(110, 100)]   // 10% justo, todavía no
    public void DesvioTolerable_NoMolesta(double areaHa, decimal catastral)
    {
        // Avisar por todo es igual de inútil que no avisar por nada.
        Assert.Null(CampaignGeometryService.AvisoPorDesvio("Lote 1", areaHa, catastral));
    }

    [Fact]
    public void SinSuperficieCatastral_NoAvisa()
    {
        // No hay contra qué comparar, y la falta del dato administrativo no es un problema
        // del polígono. Avisar acá sería mandar a revisar la geometría equivocada.
        Assert.Null(CampaignGeometryService.AvisoPorDesvio("Lote 1", 50, 0));
    }

    [Fact]
    public void ElDesvioCuentaParaLosDosLados()
    {
        // Un polígono que da de más también puede estar mal trazado.
        Assert.NotNull(CampaignGeometryService.AvisoPorDesvio("Lote 1", 150, 100));
        Assert.NotNull(CampaignGeometryService.AvisoPorDesvio("Lote 1", 50, 100));
    }

    [Fact]
    public void ElAvisoUsaLosMismosUmbralesQueElPanel()
    {
        // Si acá hubiera un umbral propio, el panel del lote podría mostrar el desvío en rojo
        // mientras el guardado no dice nada, o al revés.
        Assert.Equal(10.0, GestorOT.Shared.SurfaceDeviation.CriticalPercent);

        Assert.Null(CampaignGeometryService.AvisoPorDesvio("Lote 1", 110, 100));      // 10% justo: no
        Assert.NotNull(CampaignGeometryService.AvisoPorDesvio("Lote 1", 110.5, 100)); // pasa el umbral: sí
    }
}
