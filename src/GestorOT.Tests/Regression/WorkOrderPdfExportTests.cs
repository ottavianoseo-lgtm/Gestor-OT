using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestorOT.Tests.Regression;

public class WorkOrderPdfExportTests
{
    static WorkOrderPdfExportTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task GeneratePdf_ReturnsNonEmptyByteArray()
    {
        var wo = CreateFakeWorkOrder();
        var mockQuery = new Mock<IWorkOrderQueryService>();
        mockQuery.Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wo);

        var service = new Infrastructure.Services.WorkOrderPdfExporterService(mockQuery.Object);
        var pdf = await service.GeneratePdfAsync(Guid.NewGuid());

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }

    [Fact]
    public async Task GeneratePdf_WithOTNumber_ReturnsValidPdf()
    {
        var wo = CreateFakeWorkOrder(otNumber: "OT-001");
        var mockQuery = new Mock<IWorkOrderQueryService>();
        mockQuery.Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wo);

        var service = new Infrastructure.Services.WorkOrderPdfExporterService(mockQuery.Object);
        var pdf = await service.GeneratePdfAsync(Guid.NewGuid());

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
        Assert.Equal((byte)'%', pdf[0]); // PDF magic number
    }

    [Fact]
    public async Task GeneratePdf_WithPlannedLabors_ReturnsValidPdf()
    {
        var wo = CreateFakeWorkOrder();
        wo.Labors = new List<LaborDto>
        {
            CreateLabor("Pulverizacion", "Lote A", "Planned"),
            CreateLabor("Siembra", "Lote B", "Planned"),
            CreateLabor("Cosecha", "Lote C", "Realized")
        };

        var mockQuery = new Mock<IWorkOrderQueryService>();
        mockQuery.Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wo);

        var service = new Infrastructure.Services.WorkOrderPdfExporterService(mockQuery.Object);
        var pdf = await service.GeneratePdfAsync(Guid.NewGuid());

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }

    [Fact]
    public async Task GeneratePdf_WithSupplyApprovals_ReturnsValidPdf()
    {
        var wo = CreateFakeWorkOrder();
        wo.SupplyApprovals = new List<WorkOrderSupplyApprovalDto>
        {
            new() { SupplyName = "GLIFOSATO", TotalCalculated = 30m, ApprovedWithdrawal = 20m, WithdrawalCenter = "Galpon", SupplyUnit = "HA" },
            new() { SupplyName = "ATRAZINA", TotalCalculated = 15m, ApprovedWithdrawal = 15m, WithdrawalCenter = "Deposito", SupplyUnit = "L" }
        };

        var mockQuery = new Mock<IWorkOrderQueryService>();
        mockQuery.Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wo);

        var service = new Infrastructure.Services.WorkOrderPdfExporterService(mockQuery.Object);
        var pdf = await service.GeneratePdfAsync(Guid.NewGuid());

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }

    [Fact]
    public async Task GeneratePdf_HandlesEmptyLabors_Gracefully()
    {
        var wo = new WorkOrderDetailDto
        {
            Id = Guid.NewGuid(),
            OTNumber = "OT-EMPTY",
            Name = "OT Vacía",
            Description = "Sin labores",
            Status = "Draft",
            DueDate = DateTime.Today,
            Labors = new List<LaborDto>(),
            SupplyApprovals = new List<WorkOrderSupplyApprovalDto>()
        };

        var mockQuery = new Mock<IWorkOrderQueryService>();
        mockQuery.Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wo);

        var service = new Infrastructure.Services.WorkOrderPdfExporterService(mockQuery.Object);
        var pdf = await service.GeneratePdfAsync(Guid.NewGuid());

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }

    [Fact]
    public async Task GeneratePdf_HandlesEmptyApprovals_Gracefully()
    {
        var wo = CreateFakeWorkOrder();
        wo.SupplyApprovals = new List<WorkOrderSupplyApprovalDto>();

        var mockQuery = new Mock<IWorkOrderQueryService>();
        mockQuery.Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wo);

        var service = new Infrastructure.Services.WorkOrderPdfExporterService(mockQuery.Object);
        var pdf = await service.GeneratePdfAsync(Guid.NewGuid());

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }

    [Fact]
    public async Task GeneratePdf_Throws_WhenWorkOrderNotFound()
    {
        var mockQuery = new Mock<IWorkOrderQueryService>();
        mockQuery.Setup(q => q.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderDetailDto?)null);

        var service = new Infrastructure.Services.WorkOrderPdfExporterService(mockQuery.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GeneratePdfAsync(Guid.NewGuid()));
    }

    [Fact]
    public void IWorkOrderPdfExporterService_InterfaceExists()
    {
        var type = typeof(IWorkOrderPdfExporterService);
        Assert.True(type.IsInterface);

        var method = type.GetMethod("GeneratePdfAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<byte[]>), method!.ReturnType);
    }

    private static WorkOrderDetailDto CreateFakeWorkOrder(string? otNumber = "12")
    {
        return new WorkOrderDetailDto
        {
            Id = Guid.NewGuid(),
            OTNumber = otNumber,
            Name = "OT Franco",
            Description = "PRUEBA",
            Status = "en_progreso",
            DueDate = new DateTime(2026, 1, 28),
            AssignedTo = "AGROSERVICIOS GRANERO CHICO SA",
            Labors = new List<LaborDto>
            {
                CreateLabor("FUMIGACION TERRESTRE", "Lote Franco", "Planned"),
            },
            SupplyApprovals = new List<WorkOrderSupplyApprovalDto>
            {
                new() { SupplyName = "GLIFOSATO", TotalCalculated = 30m, ApprovedWithdrawal = 20m, WithdrawalCenter = "Galpon", SupplyUnit = "Hectárea(HA)" }
            }
        };
    }

    private static LaborDto CreateLabor(string laborType, string lotName, string status)
    {
        return new LaborDto
        {
            Id = Guid.NewGuid(),
            LaborTypeName = laborType,
            ErpActivityName = "MAIZ 505 RR2 C16",
            LotName = lotName,
            FieldName = "El Porvenir",
            Hectares = status == "Realized" ? 0 : 15m,
            Mode = status,
            Status = status,
            EstimatedDate = new DateTime(2026, 1, 28),
            CreatedAt = new DateTime(2026, 1, 28),
            AssignedTo = "ARIEU ISABEL MARÍA",
            Supplies = new List<LaborSupplyDto>
            {
                new()
                {
                    SupplyName = "GLIFOSATO",
                    SupplyUnit = "HA",
                    PlannedHectares = 10m,
                    PlannedDose = 2m,
                    PlannedTotal = 20m
                }
            }
        };
    }
}
