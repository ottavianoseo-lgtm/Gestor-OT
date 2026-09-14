using GestorOT.Infrastructure.Services;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// OT-51, matching difuso de nombres de polígono contra lotes. Estos tests fijan la regla sin
/// base de datos: qué variantes se consideran el mismo nombre y a partir de cuánta similitud se
/// propone vincular.
/// </summary>
public class LotNameMatcherTests
{
    [Theory]
    [InlineData("Lote Nº 01", "1")]
    [InlineData("Lote_1", "1")]
    [InlineData("  lote   1 ", "1")]
    [InlineData("POTRERO 3", "3")]
    [InlineData("Lote San José", "San Jose")]
    [InlineData("ÑANDÚ 2", "Nandu 2")]
    public void Normalize_EquiparaVariantes(string a, string b)
    {
        Assert.Equal(LotNameMatcher.Normalize(a), LotNameMatcher.Normalize(b));
    }

    [Fact]
    public void Score_ExactoTrasNormalizar_EsUno()
    {
        Assert.Equal(1, LotNameMatcher.Score("Lote 5", "5"));
    }

    [Fact]
    public void Score_ErrorDeTipeo_QuedaPorEncimaDelUmbralDeAuto()
    {
        // "Norrte" vs "Norte": un carácter de más sobre 6. Es el caso real que el match exacto
        // dejaba para resolver a mano.
        var score = LotNameMatcher.Score("Lote Norrte", "Lote Norte");

        Assert.True(score >= LotNameMatcher.AutoThreshold, $"score={score}");
    }

    [Fact]
    public void Score_NombresDistintos_QuedaPorDebajoDelUmbral()
    {
        Assert.True(LotNameMatcher.Score("Lote 1", "Lote 99") < LotNameMatcher.AutoThreshold);
    }

    [Fact]
    public void Score_SinNombre_EsCero()
    {
        Assert.Equal(0, LotNameMatcher.Score(null, "Lote 1"));
        Assert.Equal(0, LotNameMatcher.Score("", "Lote 1"));
    }
}
