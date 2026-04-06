using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.Status).HasMaxLength(50);
        builder.Property(e => e.AssignedTo).HasMaxLength(200);

        builder.HasOne(e => e.Lot)
            .WithMany(l => l.WorkOrders)
            .HasForeignKey(e => e.LotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Campaign)
            .WithMany(c => c.WorkOrders)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
