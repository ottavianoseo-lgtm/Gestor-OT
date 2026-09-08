using System;
using System.Collections.Generic;

namespace GestorOT.Shared;

public static class UnitHelper
{
    private static readonly HashSet<string> SurfaceUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "ha", "hta", "htas", "has", "hectarea", "hectareas", "hec", "has."
    };

    /// <summary>
    /// Checks if a given unit string represents a land surface/area unit (e.g. "HA", "HTA").
    /// </summary>
    public static bool IsSurfaceUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return false;

        var cleaned = unit.Trim().TrimEnd('.');
        return SurfaceUnits.Contains(cleaned);
    }

    /// <summary>
    /// Cleans a dose unit string (e.g. "LT/ha", "kg / ha", "bls/ha") to return only the physical supply unit ("LT", "kg", "bls").
    /// If the unit itself is a surface unit ("HA", "HTA"), returns null so a fallback can be used.
    /// </summary>
    public static string? CleanDoseUnit(string? doseUnit)
    {
        if (string.IsNullOrWhiteSpace(doseUnit))
            return null;

        var trimmed = doseUnit.Trim();

        // If it's a compound rate like "LT/ha", "kg/ha", take the numerator
        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex > 0)
        {
            var numerator = trimmed[..slashIndex].Trim();
            if (!string.IsNullOrWhiteSpace(numerator) && !IsSurfaceUnit(numerator))
            {
                return numerator;
            }
        }

        if (IsSurfaceUnit(trimmed))
            return null;

        return trimmed;
    }

    /// <summary>
    /// Determines the effective physical unit of measure of a supply item.
    /// In GestorMax ERP, UnitA is often set to "HA" (auxiliary dosing reference), while the real physical unit
    /// is in UnitB (e.g. "LT", "KG", "Bls"). This method prioritizes the physical non-surface unit.
    /// </summary>
    public static string GetEffectiveSupplyUnit(string? unitA, string? unitB = null, string? unit = null, string? fallback = "u")
    {
        // 1. If UnitA is present and NOT a surface unit, it's the primary physical unit
        if (!string.IsNullOrWhiteSpace(unitA) && !IsSurfaceUnit(unitA))
            return unitA.Trim();

        // 2. If UnitA was a surface unit (or empty), check UnitB
        if (!string.IsNullOrWhiteSpace(unitB) && !IsSurfaceUnit(unitB))
            return unitB.Trim();

        // 3. Check general Unit property
        if (!string.IsNullOrWhiteSpace(unit) && !IsSurfaceUnit(unit))
            return unit.Trim();

        // 4. If clean fallback provided, use it
        if (!string.IsNullOrWhiteSpace(fallback) && !IsSurfaceUnit(fallback))
            return fallback.Trim();

        return "u";
    }

    /// <summary>
    /// Formats the dose rate label (e.g. "LT/ha", "KG/ha") given the effective supply unit.
    /// </summary>
    public static string FormatDoseRate(string? effectiveSupplyUnit, string surfaceUnit = "ha")
    {
        var unit = string.IsNullOrWhiteSpace(effectiveSupplyUnit) || IsSurfaceUnit(effectiveSupplyUnit)
            ? "u"
            : effectiveSupplyUnit.Trim();

        return $"{unit}/{surfaceUnit}";
    }
}
