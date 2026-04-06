using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class StrategyItemConfiguration : IEntityTypeConfiguration<StrategyItem>
{
    public void Configure(EntityTypeBuilder<StrategyItem> builder)
    {
        builder.ToTable("StrategyItems", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.LaborTypeId).IsRequired();
        builder.Property(e => e.DefaultSuppliesJson).HasColumnName("DefaultSupplies").HasColumnType("jsonb");

        builder.HasOne(e => e.Strategy)
            .WithMany(s => s.Items)
            .HasForeignKey(e => e.CropStrategyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
