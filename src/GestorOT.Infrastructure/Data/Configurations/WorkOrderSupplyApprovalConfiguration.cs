using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class WorkOrderSupplyApprovalConfiguration : IEntityTypeConfiguration<WorkOrderSupplyApproval>
{
    public void Configure(EntityTypeBuilder<WorkOrderSupplyApproval> builder)
    {
        builder.ToTable("WorkOrderSupplyApprovals", "public");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TotalCalculated).HasPrecision(18, 4);
        builder.Property(e => e.ApprovedWithdrawal).HasPrecision(18, 4);
        builder.Property(e => e.RealTotalUsed).HasPrecision(18, 4);
        builder.Property(e => e.WithdrawalCenter).HasMaxLength(200);

        builder.HasOne(e => e.WorkOrder)
            .WithMany(w => w.SupplyApprovals)
            .HasForeignKey(e => e.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Supply)
            .WithMany()
            .HasForeignKey(e => e.SupplyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
