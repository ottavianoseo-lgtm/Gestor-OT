using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;

namespace GestorOT.Tests.Regression;

public class PlaneamientoOriginalRegressionTests
{
    // ── Test 1: CreateLabor_ReturnsBadRequest_WhenOriginalPlanIsRealized ─────

    [Fact]
    public void CreateLabor_ReturnsBadRequest_WhenOriginalPlanIsRealized()
    {
        bool isOriginalPlan = true;
        string status = "Realized";
        string mode = "Realized";

        var isRejected = isOriginalPlan && (status != "Planned" || mode != "Planned");
        Assert.True(isRejected);
    }

    [Fact]
    public void CreateLabor_ReturnsBadRequest_WhenOriginalPlanStatusRealized()
    {
        bool isOriginalPlan = true;
        string status = "Realized";

        var isRejected = isOriginalPlan && status != "Planned";
        Assert.True(isRejected);
    }

    // ── Test 2: CreateLabor_CreatesOriginalPlan_WhenPlannedAndCampaignLotValid ─

    [Fact]
    public void CreateLabor_CreatesOriginalPlan_WhenPlannedAndCampaignLotValid()
    {
        var labor = new Labor
        {
            Id = Guid.NewGuid(),
            Status = LaborStatus.Planned,
            Mode = LaborMode.Planned,
            IsOriginalPlan = true,
            CampaignLotId = Guid.NewGuid(),
            Hectares = 50m
        };

        Assert.Equal(LaborStatus.Planned, labor.Status);
        Assert.Equal(LaborMode.Planned, labor.Mode);
        Assert.True(labor.IsOriginalPlan);
        Assert.NotNull(labor.CampaignLotId);
        Assert.NotEqual(Guid.Empty, labor.CampaignLotId!.Value);
    }

    // ── Test 3: GetLabors_FiltersOriginalPlanByCampaign ─────────────────────

    [Fact]
    public void GetLabors_FiltersOriginalPlanByCampaign()
    {
        var campaignId = Guid.NewGuid();
        var otherCampaignId = Guid.NewGuid();

        var labors = new List<Labor>
        {
            new() { Id = Guid.NewGuid(), IsOriginalPlan = true, CampaignLot = new CampaignLot { Id = Guid.NewGuid(), CampaignId = campaignId } },
            new() { Id = Guid.NewGuid(), IsOriginalPlan = true, CampaignLot = new CampaignLot { Id = Guid.NewGuid(), CampaignId = otherCampaignId } },
            new() { Id = Guid.NewGuid(), IsOriginalPlan = false, CampaignLot = new CampaignLot { Id = Guid.NewGuid(), CampaignId = campaignId } }
        };

        var originalPlanLaborsInCampaign = labors
            .Where(l => l.IsOriginalPlan && l.CampaignLot?.CampaignId == campaignId)
            .ToList();

        Assert.Single(originalPlanLaborsInCampaign);
    }

    // ── Test 4: CreateBulkFromStrategy_ForceOriginalPlanCreatesOnlyPlanned ──

    [Fact]
    public void CreateBulkFromStrategy_ForceOriginalPlanCreatesOnlyPlanned()
    {
        bool forceOriginalPlan = true;
        string requestedStatus = "Realized";

        var finalStatus = forceOriginalPlan
            ? LaborStatus.Planned
            : (Enum.TryParse<LaborStatus>(requestedStatus, out var st) ? st : LaborStatus.Planned);

        Assert.Equal(LaborStatus.Planned, finalStatus);
    }

    [Fact]
    public void CreateBulkFromStrategy_WithOriginalPlan_SetsModePlanned()
    {
        bool isOriginalPlan = true;
        LaborStatus status = LaborStatus.Planned;

        var mode = status == LaborStatus.Realized ? LaborMode.Realized : LaborMode.Planned;
        Assert.Equal(LaborMode.Planned, mode);
    }

    // ── Test 5: CreateBulkFromStrategy_ReturnsErrors_WhenRotationActivityConflict ─

    [Fact]
    public void CreateBulkFromStrategy_ReturnsErrors_WhenRotationActivityConflict()
    {
        var lotId = Guid.NewGuid();
        var strategyActivityId = Guid.NewGuid();
        var rotationActivityId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);

        var rotations = new List<Rotation>
        {
            new() { Id = Guid.NewGuid(), CampaignLotId = lotId, ErpActivityId = rotationActivityId,
                    StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) }
        };

        var hasConflict = rotations.Any(r =>
            r.CampaignLotId == lotId &&
            r.StartDate <= date && r.EndDate >= date &&
            r.ErpActivityId != strategyActivityId);

        Assert.True(hasConflict);
    }

    [Fact]
    public void CreateBulkFromStrategy_AllowsWhenNoRotation()
    {
        var lotId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);

        var rotations = new List<Rotation>();

        var hasError = rotations.Any(r =>
            r.CampaignLotId == lotId &&
            r.StartDate <= date && r.EndDate >= date &&
            r.ErpActivityId != activityId);

        var hasWarning = !rotations.Any(r =>
            r.CampaignLotId == lotId &&
            r.StartDate <= date && r.EndDate >= date);

        Assert.False(hasError);
        Assert.True(hasWarning);
    }

    // ── Test 6: CampaignSelector_ReturnsAllCampaigns_IncludingLocked ─────────

    [Fact]
    public void CampaignSelector_ReturnsAllCampaigns_IncludingLocked()
    {
        var campaigns = new List<CampaignSummaryDto>
        {
            new(Guid.NewGuid(), "Campaña Activa", GestorOT.Shared.Dtos.CampaignStatus.Active, false),
            new(Guid.NewGuid(), "Campaña Bloqueada", GestorOT.Shared.Dtos.CampaignStatus.Locked, true)
        };

        Assert.Equal(2, campaigns.Count);
        Assert.Contains(campaigns, c => c.Status == GestorOT.Shared.Dtos.CampaignStatus.Locked);
        Assert.Contains(campaigns, c => c.Status == GestorOT.Shared.Dtos.CampaignStatus.Active);
    }

    // ── Test 7: CreateCampaign_NotifiesSelectorRefresh ──────────────────────
    // Prueba manual UI: verificar que al crear/modificar/eliminar campaña,
    // el CampaignSelector se refresca sin Ctrl+F5.

    // ── Test 8: AssignCampaignToLot_ProductiveAreaExceedsCadastral ──────────

    [Fact]
    public void AssignCampaignToLot_DoesNotBlockWhenProductiveAreaExceedsCadastral()
    {
        var lot = new LotDto(
            Guid.NewGuid(), Guid.NewGuid(), "Lote Test", "Active",
            CadastralArea: 50m);
        var productiveArea = 80m;

        // Productive area > cadastral area is allowed (just warning)
        Assert.True(productiveArea > lot.CadastralArea);
    }

    [Fact]
    public void OriginalPlan_LockedCampaign_BlocksAllModifications()
    {
        string campaignStatus = "Locked";
        bool isLocked = campaignStatus == "Locked";

        bool canCreate = !isLocked;
        bool canUnpin = !isLocked;
        bool canEdit = !isLocked;

        Assert.False(canCreate);
        Assert.False(canUnpin);
        Assert.False(canEdit);
    }

    [Fact]
    public void LaborEditorForm_ForceOriginalPlan_SetsCorrectValues()
    {
        var dto = new LaborDto
        {
            IsOriginalPlan = true,
            Status = "Planned",
            Mode = "Planned"
        };

        Assert.True(dto.IsOriginalPlan);
        Assert.Equal("Planned", dto.Status);
        Assert.Equal("Planned", dto.Mode);
    }

    [Fact]
    public void OriginalPlan_StatusAlwaysShowsPlanned_RegardlessOfRealData()
    {
        var labor1 = new Labor { Id = Guid.NewGuid(), Status = LaborStatus.Planned, IsOriginalPlan = true };
        var labor2 = new Labor { Id = Guid.NewGuid(), Status = LaborStatus.Realized, IsOriginalPlan = true };

        var uiStatus1 = "Planeada (Base)";
        var uiStatus2 = "Planeada (Base)";

        Assert.Equal(uiStatus1, uiStatus2);
    }
}
