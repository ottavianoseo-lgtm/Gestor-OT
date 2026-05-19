using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class FileAssetConfiguration : IEntityTypeConfiguration<FileAsset>
{
    public void Configure(EntityTypeBuilder<FileAsset> builder)
    {
        builder.ToTable("FileAssets", "public");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(100);
        builder.Property(e => e.Hash).HasMaxLength(256);
        builder.Property(e => e.Tags).HasMaxLength(1000);
        builder.Property(e => e.Visibility).HasMaxLength(50);
        builder.Property(e => e.UploadedBy).HasMaxLength(200);
    }
}
