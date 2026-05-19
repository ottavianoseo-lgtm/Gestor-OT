using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class LaborFileAssetConfiguration : IEntityTypeConfiguration<LaborFileAsset>
{
    public void Configure(EntityTypeBuilder<LaborFileAsset> builder)
    {
        builder.ToTable("LaborFileAssets", "public");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Labor)
            .WithMany()
            .HasForeignKey(e => e.LaborId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.FileAsset)
            .WithMany()
            .HasForeignKey(e => e.FileAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LaborId, e.FileAssetId }).IsUnique();
    }
}
