namespace GestorOT.Shared.Dtos;

public record RotationDto(
    Guid Id,
    Guid CampaignLotId,
    Guid ActivityId,
    string? ActivityName,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes
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
