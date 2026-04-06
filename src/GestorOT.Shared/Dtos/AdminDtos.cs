namespace GestorOT.Shared.Dtos;

public record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTime CreatedAt
)
{
    public UserProfileDto() : this(Guid.Empty, string.Empty, string.Empty, "Agronomist", true, DateTime.MinValue) { }
}

public record TankMixRuleDto(
    Guid Id,
    Guid ProductAId,
    Guid ProductBId,
    string Severity,
    string WarningMessage,
    string? ProductAName = null,
    string? ProductBName = null
)
{
    public TankMixRuleDto() : this(Guid.Empty, Guid.Empty, Guid.Empty, "Warning", string.Empty) { }
}

public record TankMixAlertDto(
    Guid RuleId,
    Guid ProductAId,
    string ProductAName,
    Guid ProductBId,
    string ProductBName,
    string Severity,
    string WarningMessage
);

public record AuditLogDto(
    Guid Id,
    string? UserEmail,
    string Action,
    string EntityType,
    string? EntityId,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp
);

public record TankMixValidationRequest(List<Guid> SupplyIds);
