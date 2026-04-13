using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class SharedTokenConfiguration : IEntityTypeConfiguration<SharedToken>
{
    public void Configure(EntityTypeBuilder<SharedToken> builder)
    {
        builder.ToTable("SharedTokens", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.Property(e => e.ExpiresAt);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(e => e.WorkOrder)
            .WithMany()
            .HasForeignKey(e => e.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
