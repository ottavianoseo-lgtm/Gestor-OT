namespace GestorOT.Shared.Dtos;

public record RotationDto(
    Guid Id,
    Guid CampaignLotId,
    string CropName,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes,
    Guid? SuggestedLaborTypeId,
    string? SuggestedLaborTypeName = null
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
