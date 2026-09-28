using System.ComponentModel.DataAnnotations;

namespace GestorOT.Domain.Enums;

public enum ContactRole
{
    [Display(Name = "Staff Interno")]
    InternalStaff = 0,
    [Display(Name = "Contratista")]
    Contractor = 1,
    [Display(Name = "Proveedor")]
    Supplier = 4,
    [Display(Name = "Agrónomo")]
    Agronomist = 2,
    [Display(Name = "Administrador")]
    Admin = 3,
    [Display(Name = "Super Administrador")]
    SuperAdmin = 99,
    /// <summary>
    /// Persona que llegó del ERP sin un grupo que diga si es propia o contratista. Se
    /// ofrece en los dos modos hasta que alguien la clasifique en Personas.
    /// </summary>
    [Display(Name = "Sin clasificar")]
    Unclassified = 5
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

    /// <summary>
    /// Modo de labor en el que se ofrece la persona: contratistas y proveedores en
    /// "Contratista", el personal en "Propia", y sin clasificar en los dos (null).
    /// </summary>
    public static LaborExecutionMode? ToExecutionMode(this ContactRole role) => role switch
    {
        ContactRole.Contractor or ContactRole.Supplier => LaborExecutionMode.Contractor,
        ContactRole.Unclassified => null,
        _ => LaborExecutionMode.Own
    };

    /// <summary>
    /// Rol de una persona del ERP según su "grupo de personas" de GestorMax. Si el grupo
    /// no lo dice, queda sin clasificar en vez de adivinar.
    /// </summary>
    public static ContactRole InferFromErpGroup(string? group)
    {
        if (string.IsNullOrWhiteSpace(group)) return ContactRole.Unclassified;
        var g = group.ToUpperInvariant();
        if (g.Contains("CONTRATIST") || g.Contains("TERCERO")) return ContactRole.Contractor;
        if (g.Contains("PROVEED")) return ContactRole.Supplier;
        if (g.Contains("EMPLEAD") || g.Contains("PERSONAL") || g.Contains("PROPI") || g.Contains("STAFF")) return ContactRole.InternalStaff;
        return ContactRole.Unclassified;
    }
}
