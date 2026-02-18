using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GestorOT.Data;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public class CurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var tenantHeader = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                if (Guid.TryParse(tenantHeader, out var tenantId))
                    return tenantId;
            }
            return Guid.Empty;
        }
    }
}

public class ApplicationDbContext : DbContext
{
    private readonly CurrentTenantService? _tenantService;
    private readonly GestorOT.Services.CampaignContextService? _campaignContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        CurrentTenantService? tenantService = null,
        GestorOT.Services.CampaignContextService? campaignContext = null)
        : base(options)
    {
        _tenantService = tenantService;
        _campaignContext = campaignContext;
    }

    public DbSet<Field> Fields => Set<Field>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Labor> Labors => Set<Labor>();
    public DbSet<LaborSupply> LaborSupplies => Set<LaborSupply>();
    public DbSet<CropStrategy> CropStrategies => Set<CropStrategy>();
    public DbSet<StrategyItem> StrategyItems => Set<StrategyItem>();
    public DbSet<ServiceSettlement> ServiceSettlements => Set<ServiceSettlement>();
    public DbSet<SharedToken> SharedTokens => Set<SharedToken>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<TankMixRule> TankMixRules => Set<TankMixRule>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignField> CampaignFields => Set<CampaignField>();
    public DbSet<Cultivo> Cultivos => Set<Cultivo>();
    public DbSet<PlanificacionCultivo> PlanificacionCultivos => Set<PlanificacionCultivo>();

    private Guid CurrentTenantId => _tenantService?.TenantId ?? Guid.Empty;
    private Guid? CurrentCampaignId => _campaignContext?.CurrentCampaignId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Field>(entity =>
        {
            entity.ToTable("Fields", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TotalArea).HasPrecision(18, 4);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Lot>(entity =>
        {
            entity.ToTable("Lots", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Geometry).HasColumnType("geometry(Polygon, 4326)");
            entity.HasIndex(e => e.Geometry).HasMethod("GIST");
            
            entity.HasOne(e => e.Field)
                .WithMany(f => f.Lots)
                .HasForeignKey(e => e.FieldId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("WorkOrders", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.AssignedTo).HasMaxLength(200);
            entity.HasQueryFilter(e => CurrentCampaignId == null || e.CampaignId == CurrentCampaignId);
            entity.HasOne(e => e.Lot)
                .WithMany(l => l.WorkOrders)
                .HasForeignKey(e => e.LotId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Campaign)
                .WithMany(c => c.WorkOrders)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.ToTable("Inventories", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CurrentStock).HasPrecision(18, 4);
            entity.Property(e => e.ReorderLevel).HasPrecision(18, 4);
            entity.Property(e => e.UnitA).HasMaxLength(50);
            entity.Property(e => e.UnitB).HasMaxLength(50);
            entity.Property(e => e.ConversionFactor).HasPrecision(18, 6).HasDefaultValue(1);
        });

        modelBuilder.Entity<Labor>(entity =>
        {
            entity.ToTable("Labors", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LaborType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Planned");
            entity.Property(e => e.Hectares).HasPrecision(18, 4);
            entity.Property(e => e.EffectiveArea).HasPrecision(18, 4);
            entity.Property(e => e.Rate).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.RateUnit).HasMaxLength(50).HasDefaultValue("ha");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.MetadataExterna).HasColumnType("jsonb");

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.Labors)
                .HasForeignKey(e => e.WorkOrderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Lot)
                .WithMany()
                .HasForeignKey(e => e.LotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LaborSupply>(entity =>
        {
            entity.ToTable("LaborSupplies", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlannedDose).HasPrecision(18, 6);
            entity.Property(e => e.RealDose).HasPrecision(18, 6);
            entity.Property(e => e.PlannedTotal).HasPrecision(18, 4);
            entity.Property(e => e.RealTotal).HasPrecision(18, 4);
            entity.Property(e => e.DoseUnit).HasMaxLength(100);

            entity.HasOne(e => e.Labor)
                .WithMany(l => l.Supplies)
                .HasForeignKey(e => e.LaborId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Supply)
                .WithMany()
                .HasForeignKey(e => e.SupplyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CropStrategy>(entity =>
        {
            entity.ToTable("CropStrategies", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CropType).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<StrategyItem>(entity =>
        {
            entity.ToTable("StrategyItems", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LaborType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DefaultSuppliesJson).HasColumnName("DefaultSupplies").HasColumnType("jsonb");

            entity.HasOne(e => e.Strategy)
                .WithMany(s => s.Items)
                .HasForeignKey(e => e.CropStrategyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceSettlement>(entity =>
        {
            entity.ToTable("ServiceSettlements", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalHectares).HasPrecision(18, 4);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 4);
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ErpSyncStatus).HasMaxLength(50).HasDefaultValue("Pending");

            entity.HasOne(e => e.WorkOrder)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SharedToken>(entity =>
        {
            entity.ToTable("SharedTokens", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.Property(e => e.ExpiresAt);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.WorkOrder)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50).HasDefaultValue("Agronomist");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<TankMixRule>(entity =>
        {
            entity.ToTable("TankMixRules", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(50).HasDefaultValue("Warning");
            entity.Property(e => e.WarningMessage).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.ProductA)
                .WithMany()
                .HasForeignKey(e => e.ProductAId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProductB)
                .WithMany()
                .HasForeignKey(e => e.ProductBId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(200);
            entity.Property(e => e.UserEmail).HasMaxLength(200);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("Campaigns", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Planning");
            entity.Property(e => e.BudgetTotalUSD).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.BusinessRulesJson).HasColumnName("BusinessRules").HasColumnType("jsonb");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CampaignField>(entity =>
        {
            entity.ToTable("CampaignFields", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TargetYieldTonHa).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.AllocatedHectares).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.HasIndex(e => new { e.CampaignId, e.FieldId }).IsUnique();

            entity.HasOne(e => e.Campaign)
                .WithMany(c => c.CampaignFields)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Field)
                .WithMany()
                .HasForeignKey(e => e.FieldId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Cultivo>(entity =>
        {
            entity.ToTable("Cultivos", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Variedad).HasMaxLength(200);
            entity.Property(e => e.Ciclo).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<PlanificacionCultivo>(entity =>
        {
            entity.ToTable("PlanificacionCultivos", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SuperficieSembradaHa).HasPrecision(18, 4);
            entity.Property(e => e.SuperficieGeometriaHa).HasPrecision(18, 4);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.LoteId, e.CampanaId }).IsUnique();

            entity.HasOne(e => e.Lote)
                .WithMany()
                .HasForeignKey(e => e.LoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Campana)
                .WithMany()
                .HasForeignKey(e => e.CampanaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cultivo)
                .WithMany()
                .HasForeignKey(e => e.CultivoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges()
    {
        SetTenantId();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTenantId();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTenantId()
    {
        if (CurrentTenantId == Guid.Empty) return;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>()
            .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty))
        {
            entry.Entity.TenantId = CurrentTenantId;
        }
    }
}

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class Field : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double TotalArea { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Lot> Lots { get; set; } = new List<Lot>();
}

public class Lot : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public Geometry? Geometry { get; set; }
    public Field? Field { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public class WorkOrder : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LotId { get; set; }
    public Guid? CampaignId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string OTNumber { get; set; } = string.Empty;
    public Guid? ContractorId { get; set; }
    public DateTime PlannedDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public bool StockReserved { get; set; }
    public Lot? Lot { get; set; }
    public Campaign? Campaign { get; set; }
    public ICollection<Labor> Labors { get; set; } = new List<Labor>();
}

public class Inventory : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public double ReorderLevel { get; set; }
    public string? UnitA { get; set; }
    public string? UnitB { get; set; }
    public double ConversionFactor { get; set; } = 1;
}

public class Labor : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid LotId { get; set; }
    public string LaborType { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned";
    public DateTime? ExecutionDate { get; set; }
    public decimal Hectares { get; set; }
    public decimal EffectiveArea { get; set; }
    public decimal Rate { get; set; }
    public string RateUnit { get; set; } = "ha";
    public DateTime CreatedAt { get; set; }
    public DateTime? PlannedDate { get; set; }
    public string? Notes { get; set; }
    public string? PrescriptionMapUrl { get; set; }
    public string? MachineryUsedId { get; set; }
    public string? WeatherLogJson { get; set; }
    public string? EvidencePhotosJson { get; set; }
    public string? MetadataExterna { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Lot? Lot { get; set; }
    public ICollection<LaborSupply> Supplies { get; set; } = new List<LaborSupply>();
}

public class LaborSupply
{
    public Guid Id { get; set; }
    public Guid LaborId { get; set; }
    public Guid SupplyId { get; set; }
    public decimal PlannedDose { get; set; }
    public decimal? RealDose { get; set; }
    public decimal PlannedTotal { get; set; }
    public decimal? RealTotal { get; set; }
    public string DoseUnit { get; set; } = string.Empty;
    public int TankMixOrder { get; set; }
    public bool IsSubstitute { get; set; }
    public Labor? Labor { get; set; }
    public Inventory? Supply { get; set; }
}

public class CropStrategy : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CropType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<StrategyItem> Items { get; set; } = new List<StrategyItem>();
}

public class StrategyItem
{
    public Guid Id { get; set; }
    public Guid CropStrategyId { get; set; }
    public string LaborType { get; set; } = string.Empty;
    public int DayOffset { get; set; }
    public string? DefaultSuppliesJson { get; set; }
    public CropStrategy? Strategy { get; set; }
}

public class ServiceSettlement : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public decimal TotalHectares { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string ErpSyncStatus { get; set; } = "Pending";
    public WorkOrder? WorkOrder { get; set; }
}

public class SharedToken
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TenantId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}

public class UserProfile : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Agronomist";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class TankMixRule : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductAId { get; set; }
    public Guid ProductBId { get; set; }
    public string Severity { get; set; } = "Warning";
    public string WarningMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Inventory? ProductA { get; set; }
    public Inventory? ProductB { get; set; }
}

public class AuditLog : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}

public class Cultivo : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Variedad { get; set; }
    public string? Ciclo { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlanificacionCultivo : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LoteId { get; set; }
    public Guid CampanaId { get; set; }
    public Guid CultivoId { get; set; }
    public decimal SuperficieSembradaHa { get; set; }
    public decimal SuperficieGeometriaHa { get; set; }
    public DateTime CreatedAt { get; set; }
    public Lot? Lote { get; set; }
    public Campaign? Campana { get; set; }
    public Cultivo? Cultivo { get; set; }
}

public class Campaign : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Planning";
    public decimal BudgetTotalUSD { get; set; }
    public string? BusinessRulesJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<CampaignField> CampaignFields { get; set; } = new List<CampaignField>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public class CampaignField : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid FieldId { get; set; }
    public decimal TargetYieldTonHa { get; set; }
    public decimal AllocatedHectares { get; set; }
    public Campaign? Campaign { get; set; }
    public Field? Field { get; set; }
}
