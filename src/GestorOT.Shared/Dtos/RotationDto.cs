namespace GestorOT.Shared.Dtos;

public record RotationDto(
    Guid Id,
    Guid CampaignLotId,
    Guid ActivityId,
    string? ActivityName,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes,
    string? CampaignName = null
);

public record RotationWarning(
    string LotName,
    DateOnly EndDate,
    DateOnly CampaignEndDate,
    string Message
);

public record RotationResponse(
    RotationDto Rotation,
    List<RotationWarning> Warnings
);

public enum ValidationSeverity { None, Warning, Error }

public record LaborActivityValidationResult(
    bool IsValid,
    ValidationSeverity Severity,
    string? Message,
    Guid? ExpectedActivityId,
    string? ExpectedActivityName,
    Guid? ReceivedActivityId,
    string? ReceivedActivityName
)
{
    public static LaborActivityValidationResult NoRotation(string? message = null) =>
        new(true, ValidationSeverity.Warning, message ?? "Sin rotación activa para la fecha seleccionada.", null, null, null, null);

    public static LaborActivityValidationResult Match() =>
        new(true, ValidationSeverity.None, null, null, null, null, null);

    public static LaborActivityValidationResult Conflict(Guid expectedId, string expectedName, Guid receivedId, string receivedName) =>
        new(false, ValidationSeverity.Error, $"La actividad seleccionada ({receivedName}) no coincide con el cultivo proyectado ({expectedName}).", expectedId, expectedName, receivedId, receivedName);
}
