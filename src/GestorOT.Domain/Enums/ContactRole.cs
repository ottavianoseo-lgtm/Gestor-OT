using System.ComponentModel.DataAnnotations;

namespace GestorOT.Domain.Enums;

public enum ContactRole
{
    [Display(Name = "Staff Interno")]
    InternalStaff = 0,
    [Display(Name = "Contratista")]
    Contractor = 1,
    [Display(Name = "Agrónomo")]
    Agronomist = 2,
    [Display(Name = "Administrador")]
    Admin = 3
}

public static class ContactRoleExtensions
{
    public static string GetDisplayName(this ContactRole role)
    {
        var displayAttribute = role.GetType()
            .GetField(role.ToString())?
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .FirstOrDefault() as DisplayAttribute;

        return displayAttribute?.Name ?? role.ToString();
    }
}
