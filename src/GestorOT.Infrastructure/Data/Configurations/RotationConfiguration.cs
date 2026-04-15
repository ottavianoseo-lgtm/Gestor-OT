using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class RotationConfiguration : IEntityTypeConfiguration<Rotation>
{
    public void Configure(EntityTypeBuilder<Rotation> builder)
    {
        builder.ToTable("Rotations", "public");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CropName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasOne(e => e.CampaignLot)
            .WithMany(cl => cl.Rotations)
            .HasForeignKey(e => e.CampaignLotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SuggestedLaborType)
            .WithMany()
            .HasForeignKey(e => e.SuggestedLaborTypeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
