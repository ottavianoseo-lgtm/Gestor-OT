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
    string? ExternalErpId,
    LaborExecutionMode? ExecutionMode = null
)
{
    public LaborTypeDto() : this(Guid.Empty, string.Empty, null, null) { }

    /// <summary>
    /// Nombre con el subgrupo del ERP pegado atras. El ERP repite la misma tarea en dos
    /// subgrupos (por hectarea / por UTA) con codigos distintos, asi que sin esto quedan
    /// dos filas identicas a la vista. Description guarda el subgrupo tal cual vino.
    /// </summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var subGroup = ShortSubGroup;
            return string.IsNullOrWhiteSpace(subGroup) ? Name : $"{Name} - {subGroup}";
        }
    }

    /// <summary>Subgrupo sin el prefijo "LABORES ", que es redundante en un catalogo de labores.</summary>
    [JsonIgnore]
    public string? ShortSubGroup
    {
        get
        {
            var subGroup = Description?.Trim();
            if (string.IsNullOrEmpty(subGroup)) return null;

            const string redundantPrefix = "LABORES ";
            if (subGroup.StartsWith(redundantPrefix, StringComparison.OrdinalIgnoreCase)
                && subGroup.Length > redundantPrefix.Length)
            {
                subGroup = subGroup[redundantPrefix.Length..].Trim();
            }

            return subGroup;
        }
    }
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
