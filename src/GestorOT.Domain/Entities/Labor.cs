namespace GestorOT.Domain.Entities;

public class Labor : TenantEntity
{
    public Guid? WorkOrderId { get; set; }
    public Guid LotId { get; set; }
    public Guid CampaignLotId { get; set; }
    public Guid LaborTypeId { get; set; }
    public string Status { get; set; } = "Planned";
    public DateTime? ExecutionDate { get; set; }
    public DateTime? EstimatedDate { get; set; }
    public decimal Hectares { get; set; }
    public decimal EffectiveArea { get; set; }
    public decimal Rate { get; set; }
    public string RateUnit { get; set; } = "ha";
    public decimal PlannedDose { get; set; }
    public decimal? RealizedDose { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
    public string? PrescriptionMapUrl { get; set; }
    public string? MachineryUsedId { get; set; }
    public string? WeatherLogJson { get; set; }
    public string? EvidencePhotosJson { get; set; }
    public string? MetadataExterna { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Lot? Lot { get; set; }
    public CampaignLot? CampaignLot { get; set; }
    public LaborType? Type { get; set; }
    public ICollection<LaborSupply> Supplies { get; set; } = new List<LaborSupply>();
}
