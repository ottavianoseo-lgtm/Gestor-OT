using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class CropStrategyConfiguration : IEntityTypeConfiguration<CropStrategy>
{
    public void Configure(EntityTypeBuilder<CropStrategy> builder)
    {
        builder.ToTable("CropStrategies", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.CropType).HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
