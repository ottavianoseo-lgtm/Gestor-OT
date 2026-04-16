namespace GestorOT.Shared.Dtos;

public record ErpActivityDto(Guid Id, string Name, string? ExternalErpId)
{
    public ErpActivityDto() : this(Guid.Empty, string.Empty, null) { }
}
