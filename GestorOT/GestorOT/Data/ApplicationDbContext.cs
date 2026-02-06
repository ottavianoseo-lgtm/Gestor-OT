using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GestorOT.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Field> Fields => Set<Field>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Labor> Labors => Set<Labor>();
    public DbSet<LaborSupply> LaborSupplies => Set<LaborSupply>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");

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
            
            entity.HasOne(e => e.Lot)
                .WithMany(l => l.WorkOrders)
                .HasForeignKey(e => e.LotId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.Labors)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

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
    }
}

public class Field
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double TotalArea { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Lot> Lots { get; set; } = new List<Lot>();
}

public class Lot
{
    public Guid Id { get; set; }
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public Geometry? Geometry { get; set; }
    public Field? Field { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public class WorkOrder
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public Lot? Lot { get; set; }
    public ICollection<Labor> Labors { get; set; } = new List<Labor>();
}

public class Inventory
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public double ReorderLevel { get; set; }
    public string? UnitA { get; set; }
    public string? UnitB { get; set; }
    public double ConversionFactor { get; set; } = 1;
}

public class Labor
{
    public Guid Id { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid LotId { get; set; }
    public string LaborType { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned";
    public DateTime? ExecutionDate { get; set; }
    public decimal Hectares { get; set; }
    public DateTime CreatedAt { get; set; }
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
    public Labor? Labor { get; set; }
    public Inventory? Supply { get; set; }
}
