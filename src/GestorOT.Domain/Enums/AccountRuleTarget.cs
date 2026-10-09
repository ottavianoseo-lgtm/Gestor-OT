using System.ComponentModel.DataAnnotations;

namespace GestorOT.Domain.Enums;

/// <summary>
/// A que linea del pase G4 aplica una regla contable. Labor e insumos de una misma OT imputan
/// distinto (otro comprobante, sin persona, otra moneda y lista, contrapartida en stock), asi
/// que cada uno se resuelve contra sus propias reglas.
/// </summary>
public enum AccountRuleTarget
{
    [Display(Name = "Labor")]
    Labor = 0,
    [Display(Name = "Insumo")]
    Supply = 1
}
