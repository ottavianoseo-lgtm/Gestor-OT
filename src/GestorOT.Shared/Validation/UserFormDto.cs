using System.ComponentModel.DataAnnotations;

namespace GestorOT.Shared.Validation;

public class UserFormDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 200 caracteres.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato de email no es válido.")]
    [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [RegularExpression("^(Admin|TenantAdmin|Agronomist|Contractor)$",
        ErrorMessage = "El rol debe ser Admin, TenantAdmin, Agronomist o Contractor.")]
    public string Role { get; set; } = "Agronomist";

    public bool IsActive { get; set; } = true;
}

public class ProductFormDto
{
    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 200 caracteres.")]
    public string ItemName { get; set; } = string.Empty;

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [StringLength(100, ErrorMessage = "La categoría no puede exceder 100 caracteres.")]
    public string Category { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "El stock debe ser mayor o igual a 0.")]
    public decimal CurrentStock { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El nivel de reorden debe ser mayor o igual a 0.")]
    public decimal ReorderLevel { get; set; }

    [StringLength(50, ErrorMessage = "La unidad no puede exceder 50 caracteres.")]
    public string UnitA { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "La unidad no puede exceder 50 caracteres.")]
    public string UnitB { get; set; } = string.Empty;

    [Range(0.0001, double.MaxValue, ErrorMessage = "El factor de conversión debe ser mayor a 0.")]
    public decimal ConversionFactor { get; set; } = 1;

    [Range(0, 999, ErrorMessage = "El orden de mezcla debe estar entre 0 y 999.")]
    public int TankMixOrder { get; set; }

    public ChemicalComposition? Composition { get; set; }
}

public class ChemicalComposition
{
    [StringLength(100)]
    public string ActiveIngredient { get; set; } = string.Empty;

    [Range(0, 100, ErrorMessage = "La concentración debe estar entre 0 y 100%.")]
    public decimal ConcentrationPercent { get; set; }

    [StringLength(100)]
    public string Formulation { get; set; } = string.Empty;
}
