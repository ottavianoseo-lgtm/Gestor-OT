namespace GestorOT.Shared.Dtos;

public record ErpActivityDto(Guid Id, string Name, string? ExternalErpId, bool IsActive = true)
{
    public ErpActivityDto() : this(Guid.Empty, string.Empty, null, true) { }
}
