namespace GestorOT.Shared.Dtos;

public enum CampaignStatus
{
    Active,
    Locked
}

public record CampaignDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    CampaignStatus Status,
    decimal BudgetTotalUSD,
    string? BusinessRulesJson,
    DateTime CreatedAt,
    List<CampaignFieldDto>? Fields = null
)
{
    public CampaignDto() : this(Guid.Empty, string.Empty, DateOnly.MinValue, DateOnly.MinValue, true, CampaignStatus.Active, 0, null, DateTime.MinValue, null) { }
}

public record CampaignFieldDto(
    Guid CampaignId,
    Guid FieldId,
    string? FieldName,
    decimal TargetYieldTonHa,
    decimal AllocatedHectares
)
{
    public CampaignFieldDto() : this(Guid.Empty, Guid.Empty, null, 0, 0) { }
}

public record CampaignSummaryDto(
    Guid Id,
    string Name,
    CampaignStatus Status,
    bool IsActive
)
{
    public CampaignSummaryDto() : this(Guid.Empty, string.Empty, CampaignStatus.Active, true) { }
}

public record CampaignLotDto(
    Guid Id,
    Guid CampaignId,
    Guid LotId,
    string? LotName,
    string? FieldName,
    decimal CadastralArea,
    decimal ProductiveArea,
    Guid? CropId
)
{
    public CampaignLotDto() : this(Guid.Empty, Guid.Empty, Guid.Empty, null, null, 0, 0, null) { }
}

public record ImportLotsRequest(
    Guid PreviousCampaignId,
    bool UseProductiveAreaFromPrevious = false
)
{
    public ImportLotsRequest() : this(Guid.Empty, false) { }
}

/// <summary>Request for batch-assigning all lots of a field to a campaign.</summary>
public record BatchAssignLotsRequest(Guid FieldId)
{
    public BatchAssignLotsRequest() : this(Guid.Empty) { }
}

/// <summary>Result of the batch assign operation.</summary>
public record BatchAssignLotsResult(int Assigned, int Skipped);

