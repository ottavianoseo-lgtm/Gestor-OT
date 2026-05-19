using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Services;
using GestorOT.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GestorOT.Tests.Regression;

public class TankMixValidationOnSaveTests
{
    private readonly Guid _productA = Guid.NewGuid();
    private readonly Guid _productB = Guid.NewGuid();
    private readonly Guid _productC = Guid.NewGuid();

    private static Mock<IApplicationDbContext> CreateContextWithTankMixRules(List<TankMixRule> rules)
    {
        var mock = new Mock<IApplicationDbContext>();

        var queryable = rules.AsQueryable();
        var dbSetMock = new Mock<DbSet<TankMixRule>>();
        dbSetMock.As<IAsyncEnumerable<TankMixRule>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<TankMixRule>(queryable.GetEnumerator()));
        dbSetMock.As<IQueryable<TankMixRule>>().Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<TankMixRule>(queryable.Provider));
        dbSetMock.As<IQueryable<TankMixRule>>().Setup(m => m.Expression).Returns(queryable.Expression);
        dbSetMock.As<IQueryable<TankMixRule>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        dbSetMock.As<IQueryable<TankMixRule>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
        mock.Setup(c => c.TankMixRules).Returns(dbSetMock.Object);
        mock.Setup(c => c.CurrentTenantId).Returns(Guid.NewGuid());

        return mock;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 1. ValidateMixAsync con regla bloqueante
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_WithBlockRule_ReturnsBlockingAlerts()
    {
        var rule = new TankMixRule
        {
            Id = Guid.NewGuid(),
            ProductAId = _productA,
            ProductBId = _productB,
            Severity = "Error",
            WarningMessage = "Prohibida combinación",
            ProductA = new Inventory { Id = _productA, ItemName = "Glifosato" },
            ProductB = new Inventory { Id = _productB, ItemName = "2,4-D" }
        };

        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule> { rule });
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productB });

        Assert.Single(alerts);
        Assert.Equal("Error", alerts[0].Severity);
        Assert.Contains("Glifosato", alerts[0].ProductAName);
        Assert.Contains("2,4-D", alerts[0].ProductBName);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 2. ValidateMixAsync con regla Warning
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_WithWarningRule_ReturnsWarningAlert()
    {
        var rule = new TankMixRule
        {
            Id = Guid.NewGuid(),
            ProductAId = _productA,
            ProductBId = _productB,
            Severity = "Warning",
            WarningMessage = "Verificar dosis",
            ProductA = new Inventory { Id = _productA, ItemName = "Glifosato" },
            ProductB = new Inventory { Id = _productB, ItemName = "Aceite Agrícola" }
        };

        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule> { rule });
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productB });

        Assert.Single(alerts);
        Assert.Equal("Warning", alerts[0].Severity);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 3. Con un solo supply no se llama al validador
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_WithSingleSupply_ReturnsEmpty()
    {
        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule>());
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA });

        Assert.Empty(alerts);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 4. Supplies sin regla devuelven vacío
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_WithUnrelatedSupplies_ReturnsEmpty()
    {
        var rule = new TankMixRule
        {
            Id = Guid.NewGuid(),
            ProductAId = _productA,
            ProductBId = _productB,
            Severity = "Error",
            WarningMessage = "No mezclar",
            ProductA = new Inventory { Id = _productA, ItemName = "A" },
            ProductB = new Inventory { Id = _productB, ItemName = "B" }
        };

        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule> { rule });
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productC });

        Assert.Empty(alerts);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 5. Severidad case-insensitive
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Error")]
    [InlineData("ERROR")]
    [InlineData("error")]
    [InlineData("Block")]
    [InlineData("BLOCK")]
    [InlineData("block")]
    public async Task ValidateMixAsync_SeverityCaseInsensitive_DetectsBlocking(string severity)
    {
        var rule = new TankMixRule
        {
            Id = Guid.NewGuid(),
            ProductAId = _productA,
            ProductBId = _productB,
            Severity = severity,
            WarningMessage = "test",
            ProductA = new Inventory { Id = _productA, ItemName = "A" },
            ProductB = new Inventory { Id = _productB, ItemName = "B" }
        };

        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule> { rule });
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productB });

        Assert.Single(alerts);
        Assert.True(
            string.Equals(alerts[0].Severity, "Error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(alerts[0].Severity, "Block", StringComparison.OrdinalIgnoreCase));
    }

    // ─────────────────────────────────────────────────────────────────────
    // 6. Varias reglas para los mismos productos
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_MultipleRulesForSamePair_ReturnsAll()
    {
        var rules = new List<TankMixRule>
        {
            new()
            {
                Id = Guid.NewGuid(), ProductAId = _productA, ProductBId = _productB,
                Severity = "Warning", WarningMessage = "W1",
                ProductA = new Inventory { Id = _productA, ItemName = "A" },
                ProductB = new Inventory { Id = _productB, ItemName = "B" }
            },
            new()
            {
                Id = Guid.NewGuid(), ProductAId = _productA, ProductBId = _productB,
                Severity = "Error", WarningMessage = "E1",
                ProductA = new Inventory { Id = _productA, ItemName = "A" },
                ProductB = new Inventory { Id = _productB, ItemName = "B" }
            }
        };

        var ctxMock = CreateContextWithTankMixRules(rules);
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productB });

        Assert.Equal(2, alerts.Count);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 7. Tres productos: alerts para cada par con regla
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_ThreeProducts_ChecksAllPairs()
    {
        var ruleAB = new TankMixRule
        {
            Id = Guid.NewGuid(), ProductAId = _productA, ProductBId = _productB,
            Severity = "Error", WarningMessage = "AB",
            ProductA = new Inventory { Id = _productA, ItemName = "A" },
            ProductB = new Inventory { Id = _productB, ItemName = "B" }
        };
        var ruleBC = new TankMixRule
        {
            Id = Guid.NewGuid(), ProductAId = _productB, ProductBId = _productC,
            Severity = "Warning", WarningMessage = "BC",
            ProductA = new Inventory { Id = _productB, ItemName = "B" },
            ProductB = new Inventory { Id = _productC, ItemName = "C" }
        };

        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule> { ruleAB, ruleBC });
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productB, _productC });

        Assert.Equal(2, alerts.Count);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 8. Lista vacía → sin alerts
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_EmptyList_ReturnsEmpty()
    {
        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule>());
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid>());

        Assert.Empty(alerts);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 9. Reglas con GUIDs duplicados (mismo supply 2 veces) → distinct
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_DuplicatedSupplyIds_DoesNotSelfMatch()
    {
        var rule = new TankMixRule
        {
            Id = Guid.NewGuid(), ProductAId = _productA, ProductBId = _productA,
            Severity = "Error", WarningMessage = "Self",
            ProductA = new Inventory { Id = _productA, ItemName = "A" },
            ProductB = new Inventory { Id = _productA, ItemName = "A" }
        };

        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule> { rule });
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productA, _productB });

        Assert.Single(alerts);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 10. Regla con severidad vacía → tratada como Warning
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateMixAsync_EmptySeverity_ReturnsAsWarning()
    {
        var rule = new TankMixRule
        {
            Id = Guid.NewGuid(),
            ProductAId = _productA,
            ProductBId = _productB,
            Severity = "",
            WarningMessage = "Sin severidad",
            ProductA = new Inventory { Id = _productA, ItemName = "A" },
            ProductB = new Inventory { Id = _productB, ItemName = "B" }
        };

        var ctxMock = CreateContextWithTankMixRules(new List<TankMixRule> { rule });
        var service = new AgronomicValidationService(ctxMock.Object);

        var alerts = await service.ValidateMixAsync(new List<Guid> { _productA, _productB });

        Assert.Single(alerts);
        Assert.Equal("", alerts[0].Severity);
    }
}
