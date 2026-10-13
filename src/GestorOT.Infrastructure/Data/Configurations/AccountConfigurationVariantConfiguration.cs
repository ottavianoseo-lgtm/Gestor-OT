using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class AccountConfigurationVariantConfiguration : IEntityTypeConfiguration<AccountConfigurationVariant>
{
    public void Configure(EntityTypeBuilder<AccountConfigurationVariant> builder)
    {
        builder.ToTable("AccountConfigurationVariants", "public");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.AccountConfiguration)
            .WithMany(c => c.Variants)
            .HasForeignKey(e => e.AccountConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ErpActivity)
            .WithMany()
            .HasForeignKey(e => e.ErpActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una sola imputacion por actividad dentro de la regla.
        builder.HasIndex(e => new { e.AccountConfigurationId, e.ErpActivityId }).IsUnique();
    }
}
