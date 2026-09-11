using GestorOT.Shared;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// OT-39: comparacion entre la superficie que sale del poligono y la catastral declarada.
/// El caso que importa no es el porcentaje sino el lote sin catastral cargada: ahi la
/// comparacion no existe, y mostrar "100% de desvio" mandaria a revisar la geometria
/// equivocada.
/// </summary>
public class SurfaceDeviationTests
{
    [Fact]
    public void SinSuperficieCatastral_NoSeCompara()
    {
        var r = SurfaceDeviation.Compare(100, 0);

        Assert.False(r.CanCompare);
        Assert.Equal(SurfaceDeviationLevel.Unknown, r.Level);
        Assert.Equal(0, r.DeviationPercent);
    }

    [Fact]
    public void CatastralNegativa_TampocoSeCompara()
    {
        Assert.False(SurfaceDeviation.Compare(100, -5).CanCompare);
    }

    [Theory]
    [InlineData(100, 100, SurfaceDeviationLevel.Ok)]        // exacto
    [InlineData(102, 100, SurfaceDeviationLevel.Ok)]        // 2%
    [InlineData(103, 100, SurfaceDeviationLevel.Warning)]   // justo en el umbral
    [InlineData(110, 100, SurfaceDeviationLevel.Warning)]   // 10%, todavia ambar
    [InlineData(110.1, 100, SurfaceDeviationLevel.Critical)]
    [InlineData(90, 100, SurfaceDeviationLevel.Warning)]    // el desvio cuenta para abajo igual
    [InlineData(80, 100, SurfaceDeviationLevel.Critical)]
    public void ElSemaforoRespetaLosUmbrales(double gis, decimal catastral, SurfaceDeviationLevel esperado)
    {
        Assert.Equal(esperado, SurfaceDeviation.Compare(gis, catastral).Level);
    }

    [Fact]
    public void LaDiferenciaConservaElSigno()
    {
        // El signo es la mitad de la informacion: no es lo mismo que sobre a que falte.
        Assert.Equal(-8, SurfaceDeviation.Compare(92, 100).DifferenceHa, 3);
        Assert.Equal(8, SurfaceDeviation.Compare(108, 100).DifferenceHa, 3);
    }

    [Fact]
    public void ElPorcentajeSeMideContraLaCatastral()
    {
        // La declarada es la referencia, no el promedio ni la dibujada.
        var r = SurfaceDeviation.Compare(75, 50);

        Assert.Equal(25, r.DifferenceHa, 3);
        Assert.Equal(50, r.DeviationPercent, 3);
    }
}
