using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class CampaignLotConfiguration : IEntityTypeConfiguration<CampaignLot>
{
    public void Configure(EntityTypeBuilder<CampaignLot> builder)
    {
        builder.ToTable("CampaignLots", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProductiveArea).HasPrecision(18, 4);
        builder.Property(e => e.Geometry).HasColumnType("geometry(Geometry, 4326)");
        builder.HasIndex(e => new { e.CampaignId, e.LotId }).IsUnique();

        builder.HasOne(e => e.Campaign)
            .WithMany(c => c.CampaignLots)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Lot)
            .WithMany(l => l.CampaignLots)
            .HasForeignKey(e => e.LotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
