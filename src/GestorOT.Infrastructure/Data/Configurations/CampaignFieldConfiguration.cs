using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class CampaignFieldConfiguration : IEntityTypeConfiguration<CampaignField>
{
    public void Configure(EntityTypeBuilder<CampaignField> builder)
    {
        builder.ToTable("CampaignFields", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TargetYieldTonHa).HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(e => e.AllocatedHectares).HasPrecision(18, 4).HasDefaultValue(0m);
        builder.HasIndex(e => new { e.CampaignId, e.FieldId }).IsUnique();

        builder.HasOne(e => e.Campaign)
            .WithMany(c => c.CampaignFields)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Field)
            .WithMany()
            .HasForeignKey(e => e.FieldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
