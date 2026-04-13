using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("Lots", "public");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Status).HasMaxLength(50);
        builder.Property(e => e.Geometry).HasColumnType("geometry(Polygon, 4326)");
        builder.HasIndex(e => e.Geometry).HasMethod("GIST");
        builder.Property(e => e.CadastralArea).HasPrecision(18, 4);
        builder.HasOne(e => e.Field)
            .WithMany(f => f.Lots)
            .HasForeignKey(e => e.FieldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
