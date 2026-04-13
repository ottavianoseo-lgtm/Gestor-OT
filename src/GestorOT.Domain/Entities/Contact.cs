using GestorOT.Domain.Enums;

namespace GestorOT.Domain.Entities;

public class Contact : TenantEntity
{
    public string FullName { get; set; } = string.Empty;
    public string? LegalName { get; set; } // Razón Social
    public string? VatNumber { get; set; } // CUIT/CUIL
    public string? ExternalErpId { get; set; }
    public Guid? ErpPersonId { get; set; } // Link to master directory
    public string? Email { get; set; }
    public string? Position { get; set; }
    public ContactRole Role { get; set; } = ContactRole.InternalStaff;
    
    // Navigation
    public ErpPerson? ErpPerson { get; set; }
}
