using System.ComponentModel.DataAnnotations;

namespace GestorOT.Domain.Enums;

/// <summary>
/// Modo de ejecución de un tipo de labor. El ERP guarda dos códigos distintos para la
/// misma labor conceptual (uno propio y uno de contratista), así que cada LaborType
/// pertenece a uno solo. Null significa sin clasificar: los tipos activados antes de
/// que existiera este campo quedan así y se muestran en ambos modos hasta marcarlos.
/// </summary>
public enum LaborExecutionMode
{
    [Display(Name = "Propia")]
    Own = 0,
    [Display(Name = "Contratista")]
    Contractor = 1
}

public static class LaborExecutionModeExtensions
{
    public static string GetDisplayName(this LaborExecutionMode mode)
    {
        var displayAttribute = mode.GetType()
            .GetField(mode.ToString())?
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .FirstOrDefault() as DisplayAttribute;

        return displayAttribute?.Name ?? mode.ToString();
    }

    /// <summary>
    /// Un tipo sirve para el modo pedido si coincide o si todavía no está clasificado.
    /// </summary>
    public static bool MatchesFilter(this LaborExecutionMode? typeMode, LaborExecutionMode requested)
        => typeMode is null || typeMode == requested;

    /// <summary>
    /// Deduce el modo del subgrupo de conceptos del ERP, que ya lo trae escrito:
    /// "LABORES POR HECTAREA (CONTRATISTA)" y "LABORES POR UTA (MAQ PROPIA)". Devuelve null
    /// si el subgrupo no dice nada, para no adivinar.
    /// </summary>
    public static LaborExecutionMode? InferFromErpSubGroup(string? subGroup)
    {
        if (string.IsNullOrWhiteSpace(subGroup)) return null;

        var s = subGroup.ToUpperInvariant();

        if (s.Contains("CONTRATISTA") || s.Contains("TERCERO")) return LaborExecutionMode.Contractor;
        if (s.Contains("PROPIA") || s.Contains("PROPIO")) return LaborExecutionMode.Own;

        return null;
    }
}
