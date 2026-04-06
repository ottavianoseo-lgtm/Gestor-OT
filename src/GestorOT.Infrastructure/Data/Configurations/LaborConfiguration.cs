using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class LaborConfiguration : IEntityTypeConfiguration<Labor>
{
    public void Configure(EntityTypeBuilder<Labor> builder)
    {
        builder.ToTable("Labors", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.LaborTypeId).IsRequired();
        builder.HasOne(e => e.Type)
            .WithMany()
            .HasForeignKey(e => e.LaborTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Planned");
        builder.Property(e => e.Hectares).HasPrecision(18, 4);
        builder.Property(e => e.EffectiveArea).HasPrecision(18, 4);
        builder.Property(e => e.Rate).HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(e => e.RateUnit).HasMaxLength(50).HasDefaultValue("ha");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.MetadataExterna).HasColumnType("jsonb");

        builder.HasOne(e => e.WorkOrder)
            .WithMany(w => w.Labors)
            .HasForeignKey(e => e.WorkOrderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Lot)
            .WithMany()
            .HasForeignKey(e => e.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
