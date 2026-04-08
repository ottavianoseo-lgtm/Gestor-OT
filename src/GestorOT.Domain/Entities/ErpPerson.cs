using System;

namespace GestorOT.Domain.Entities;

public class ErpPerson : TenantEntity
{
    public string ExternalErpId { get; set; } = string.Empty; // codPersona
    public string FullName { get; set; } = string.Empty; // persona
    public string? Alias { get; set; }
    public string? VatNumber { get; set; } // nroDocumento
    public string? PersonType { get; set; } // tipoPersonaAFIP
    public string? DocumentType { get; set; } // tipoDocumentoAFIP
    public string? Country { get; set; } // paisAFIP
    public string? ResponsibleTax { get; set; } // tipoResponsableIVA
    public string? Group { get; set; } // grupoPersonas
    public bool Enabled { get; set; } // habilitado
    public DateTime LastSyncDate { get; set; } = DateTime.UtcNow;
    public bool IsActivated { get; set; } // Flag to track if it's already a Contact
    public Guid? LinkedContactId { get; set; }
}
