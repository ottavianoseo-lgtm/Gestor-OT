using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class LaborSupplyConfiguration : IEntityTypeConfiguration<LaborSupply>
{
    public void Configure(EntityTypeBuilder<LaborSupply> builder)
    {
        builder.ToTable("LaborSupplies", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlannedDose).HasPrecision(18, 6);
        builder.Property(e => e.RealDose).HasPrecision(18, 6);
        builder.Property(e => e.PlannedTotal).HasPrecision(18, 4);
        builder.Property(e => e.RealTotal).HasPrecision(18, 4);
        builder.Property(e => e.PlannedHectares).HasPrecision(18, 4);
        builder.Property(e => e.RealHectares).HasPrecision(18, 4);
        builder.Property(e => e.CalculatedDose).HasPrecision(18, 6);
        builder.Property(e => e.CalculatedTotal).HasPrecision(18, 4);
        builder.Property(e => e.UnitOfMeasure).HasMaxLength(100);

        builder.HasOne(e => e.Labor)
            .WithMany(l => l.Supplies)
            .HasForeignKey(e => e.LaborId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Supply)
            .WithMany()
            .HasForeignKey(e => e.SupplyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
