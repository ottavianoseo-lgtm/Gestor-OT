using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class TankMixRuleConfiguration : IEntityTypeConfiguration<TankMixRule>
{
    public void Configure(EntityTypeBuilder<TankMixRule> builder)
    {
        builder.ToTable("TankMixRules", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Severity).IsRequired().HasMaxLength(50).HasDefaultValue("Warning");
        builder.Property(e => e.WarningMessage).IsRequired().HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(e => e.ProductA)
            .WithMany()
            .HasForeignKey(e => e.ProductAId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ProductB)
            .WithMany()
            .HasForeignKey(e => e.ProductBId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
