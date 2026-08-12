using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ICampaignContextService? _campaignContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IHttpContextAccessor? httpContextAccessor = null,
        ICampaignContextService? campaignContext = null)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _campaignContext = campaignContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Field> Fields => Set<Field>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Labor> Labors => Set<Labor>();
    public DbSet<LaborSupply> LaborSupplies => Set<LaborSupply>();
    public DbSet<CropStrategy> CropStrategies => Set<CropStrategy>();
    public DbSet<StrategyItem> StrategyItems => Set<StrategyItem>();
    public DbSet<LaborType> LaborTypes => Set<LaborType>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<SharedToken> SharedTokens => Set<SharedToken>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<TankMixRule> TankMixRules => Set<TankMixRule>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignLot> CampaignLots => Set<CampaignLot>();
    public DbSet<Rotation> Rotations => Set<Rotation>();
    public DbSet<CampaignField> CampaignFields => Set<CampaignField>();
    public DbSet<ErpPerson> ErpPeople => Set<ErpPerson>();
    public DbSet<ErpActivity> ErpActivities => Set<ErpActivity>();
    public DbSet<ErpConcept> ErpConcepts => Set<ErpConcept>();
    public DbSet<WorkOrderStatus> WorkOrderStatuses => Set<WorkOrderStatus>();
    public DbSet<LaborAttachment> LaborAttachments => Set<LaborAttachment>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<LaborFileAsset> LaborFileAssets => Set<LaborFileAsset>();
    public DbSet<WorkOrderSupplyApproval> WorkOrderSupplyApprovals => Set<WorkOrderSupplyApproval>();
    public DbSet<PaseLote> PasesLote => Set<PaseLote>();
    public DbSet<PaseImputacion> PasesImputacion => Set<PaseImputacion>();
    public DbSet<AccountConfiguration> AccountConfigurations => Set<AccountConfiguration>();

    public Guid CurrentTenantId
    {
        get
        {
            if (_httpContextAccessor?.HttpContext != null)
            {
                // 1° Header X-Tenant-ID: permite que el frontend cambie el tenant activo
                // independientemente del tenant fijo en el JWT.
                var tenantHeader = _httpContextAccessor.HttpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
                if (Guid.TryParse(tenantHeader, out var headerTenantId) && headerTenantId != Guid.Empty)
                    return headerTenantId;

                // 2° Claim "tenant_id" del JWT: fallback para usuarios sin header explícito.
                // Guid.Empty en el claim indica SuperAdmin (acceso global, sin filtro de tenant).
                var claimTenant = _httpContextAccessor.HttpContext.User?.FindFirst("tenant_id")?.Value;
                if (Guid.TryParse(claimTenant, out var userTenantId))
                    return userTenantId; // Puede ser Guid.Empty para SuperAdmin
            }
            return Guid.Empty;
        }
    }

    private Guid? CurrentCampaignId => _campaignContext?.CurrentCampaignId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.Entity<Field>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Lot>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<CampaignLot>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Rotation>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Labor>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });


        modelBuilder.Entity<CropStrategy>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<LaborType>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<TankMixRule>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<CampaignField>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<SharedToken>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ErpPerson>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ErpActivity>(entity =>
        {
            entity.HasQueryFilter(e => e.TenantId == Guid.Empty || CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ErpConcept>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrderStatus>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<FileAsset>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<LaborFileAsset>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<LaborAttachment>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<WorkOrderSupplyApproval>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<LaborSupply>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<StrategyItem>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<PaseLote>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<PaseImputacion>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<AccountConfiguration>(entity =>
        {
            entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        });

        // Global DateTime UTC Converter for Npgsql 6.0+
        var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()),
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
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
        var tenantId = CurrentTenantId;
        if (tenantId == Guid.Empty) return;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Prevent TenantId from being changed to Empty if it was already set
                if (entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Property(x => x.TenantId).IsModified = false;
                }
            }
        }
    }
}
