using GestorOT.Domain.Enums;
using System.Text.Json.Serialization;

namespace GestorOT.Shared.Dtos;

public record ContactDto(
    Guid Id,
    string FullName,
    string? ExternalErpId,
    string? Email,
    string? Position,
    string? LegalName = null,
    string? VatNumber = null,
    ContactRole Role = ContactRole.InternalStaff
)
{
    public ContactDto() : this(Guid.Empty, string.Empty, null, null, null, null, null, ContactRole.InternalStaff) { }
}

public record LaborTypeDto(
    Guid Id,
    string Name,
    string? Description,
    string? ExternalErpId
)
{
    public LaborTypeDto() : this(Guid.Empty, string.Empty, null, null) { }
}

public record CurrencyDto(
    [property: JsonPropertyName("codMoneda")] string Code,
    [property: JsonPropertyName("moneda")] string Name,
    [property: JsonPropertyName("simbolo")] string Symbol,
    [property: JsonPropertyName("habilitado")] bool Enabled,
    [property: JsonPropertyName("codInternoMonedaAFIP")] string? AfipCode = null
)
{
    public CurrencyDto() : this(string.Empty, string.Empty, string.Empty, true) { }
}

public record ErpPersonDto(
    Guid Id,
    string ExternalId,
    string FullName,
    string? Position,
    string? VatNumber,
    bool IsActiveContact,
    Guid? LinkedContactId
)
{
    public ErpPersonDto() : this(Guid.Empty, string.Empty, string.Empty, null, null, false, null) { }
}

public class ActivateContactRequest
{
    public Guid ErpPersonId { get; set; }
    public ContactRole Role { get; set; } = ContactRole.InternalStaff;
}
