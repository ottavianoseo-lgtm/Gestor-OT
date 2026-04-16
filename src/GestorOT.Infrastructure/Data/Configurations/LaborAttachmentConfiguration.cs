using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class LaborAttachmentConfiguration : IEntityTypeConfiguration<LaborAttachment>
{
    public void Configure(EntityTypeBuilder<LaborAttachment> builder)
    {
        builder.ToTable("LaborAttachments", "public");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(100);

        builder.HasOne(e => e.Labor)
            .WithMany()
            .HasForeignKey(e => e.LaborId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
