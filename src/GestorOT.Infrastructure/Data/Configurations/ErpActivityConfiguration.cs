using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorOT.Infrastructure.Data.Configurations;

public class ErpActivityConfiguration : IEntityTypeConfiguration<ErpActivity>
{
    public void Configure(EntityTypeBuilder<ErpActivity> builder)
    {
        builder.ToTable("ErpActivities", "public");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ExternalErpId).HasMaxLength(100);

        builder.HasData(
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e01"), Name = "Trigo", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e02"), Name = "Sorgo", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e03"), Name = "Avena", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e04"), Name = "Maní", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e05"), Name = "Camelina", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e06"), Name = "Alpiste", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e07"), Name = "Girasol", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e08"), Name = "Girasol alto oleico", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e09"), Name = "Soja 1º", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e10"), Name = "Soja 2º", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e11"), Name = "Papa", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e12"), Name = "Cebada cervezera", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e13"), Name = "Cebada forrajera", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e14"), Name = "Maíz", TenantId = Guid.Empty },
            new ErpActivity { Id = new Guid("5f96e4e0-0b6e-4f0e-8d8a-9f8e8e8e8e15"), Name = "Maíz tardío", TenantId = Guid.Empty }
        );
    }
}
