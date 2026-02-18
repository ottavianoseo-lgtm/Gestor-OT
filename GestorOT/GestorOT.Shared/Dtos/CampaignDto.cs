namespace GestorOT.Shared.Dtos;

public enum CampaignStatus
{
    Planning,
    Active,
    Locked
}

public record CampaignDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    CampaignStatus Status,
    decimal BudgetTotalUSD,
    string? BusinessRulesJson,
    DateTime CreatedAt,
    List<CampaignFieldDto>? Fields = null
)
{
    public CampaignDto() : this(Guid.Empty, string.Empty, DateOnly.MinValue, null, true, CampaignStatus.Planning, 0, null, DateTime.MinValue, null) { }
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
    public CampaignSummaryDto() : this(Guid.Empty, string.Empty, CampaignStatus.Planning, true) { }
}

public record CropDto(
    Guid Id,
    string Name,
    string? Type,
    DateTime CreatedAt
)
{
    public CropDto() : this(Guid.Empty, string.Empty, null, DateTime.MinValue) { }
}

public record CampaignPlotDto(
    Guid Id,
    Guid CampaignId,
    Guid PlotId,
    string? PlotName,
    string? FieldName,
    Guid? CropId,
    string? CropName,
    decimal ProductiveSurfaceHa,
    decimal? CatastralSurfaceHa,
    DateOnly? EstimatedStartDate,
    DateOnly? EstimatedEndDate
)
{
    public CampaignPlotDto() : this(Guid.Empty, Guid.Empty, Guid.Empty, null, null, null, null, 0, null, null, null) { }
}

public record CampaignPlotSaveDto(
    Guid PlotId,
    Guid? CropId,
    decimal ProductiveSurfaceHa,
    DateOnly? EstimatedStartDate,
    DateOnly? EstimatedEndDate
)
{
    public CampaignPlotSaveDto() : this(Guid.Empty, null, 0, null, null) { }
}

public record PlotHistoryDto(
    Guid CampaignId,
    string CampaignName,
    DateOnly CampaignStart,
    DateOnly? CampaignEnd,
    string? CropName,
    decimal ProductiveSurfaceHa,
    DateOnly? EstimatedStartDate,
    DateOnly? EstimatedEndDate
)
{
    public PlotHistoryDto() : this(Guid.Empty, string.Empty, DateOnly.MinValue, null, null, 0, null, null) { }
}
