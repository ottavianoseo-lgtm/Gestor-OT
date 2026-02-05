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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<Field>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TotalArea).HasPrecision(18, 4);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Lot>(entity =>
        {
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
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CurrentStock).HasPrecision(18, 4);
            entity.Property(e => e.ReorderLevel).HasPrecision(18, 4);
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
    public Polygon? Geometry { get; set; }
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
}

public class Inventory
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public double ReorderLevel { get; set; }
}
