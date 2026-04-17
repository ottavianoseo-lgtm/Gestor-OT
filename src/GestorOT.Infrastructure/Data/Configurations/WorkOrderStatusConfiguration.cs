using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class WorkOrderStatusConfiguration : IEntityTypeConfiguration<WorkOrderStatus>
{
    public void Configure(EntityTypeBuilder<WorkOrderStatus> builder)
    {
        builder.ToTable("WorkOrderStatuses", "public");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ColorHex).HasMaxLength(10);
    }
}
