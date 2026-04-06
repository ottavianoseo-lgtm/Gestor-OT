using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.CurrentStock).HasPrecision(18, 4);
        builder.Property(e => e.ReorderLevel).HasPrecision(18, 4);
        builder.Property(e => e.UnitA).HasMaxLength(50);
        builder.Property(e => e.UnitB).HasMaxLength(50);
        builder.Property(e => e.ConversionFactor).HasPrecision(18, 6).HasDefaultValue(1);
    }
}
