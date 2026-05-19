namespace GestorOT.Domain.Entities;

public class LaborFileAsset : TenantEntity
{
    public Guid LaborId { get; set; }
    public Guid FileAssetId { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public Labor? Labor { get; set; }
    public FileAsset? FileAsset { get; set; }
}
