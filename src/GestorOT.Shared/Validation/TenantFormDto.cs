using System.ComponentModel.DataAnnotations;

namespace GestorOT.Shared.Validation;

public class TenantFormDto
{
    [Required(ErrorMessage = "El nombre de la empresa es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Gestor Max API Key")]
    public string? GestorMaxApiKey { get; set; }

    [Display(Name = "Gestor Max Database ID")]
    public string? GestorMaxDatabaseId { get; set; }

    public bool IsActive { get; set; } = true;
}
