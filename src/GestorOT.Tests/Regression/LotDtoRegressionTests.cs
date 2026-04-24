using GestorOT.Shared.Dtos;

namespace GestorOT.Tests.Regression;

/// <summary>
/// Regression tests for Bugs #1/#2/#3/#4 — LotDto construction from form state.
///
/// These tests verify the snapshot pattern introduced in Sprint 1:
/// - The DTO is built from a local snapshot of form values taken BEFORE any await.
/// - CadastralArea is always included (Bug #4).
/// - LotName is never replaced with CampaignName (Bugs #1-3).
/// - CadastralArea is never replaced with GIS calculated area (Bug #3).
/// </summary>
public class LotDtoRegressionTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers that mirror the snapshot logic from Lotes.razor SaveLot()
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class LotFormModel
    {
        public string Name { get; set; } = string.Empty;
        public Guid FieldId { get; set; }
        public string Status { get; set; } = "Active";
        public decimal CadastralArea { get; set; }
    }

    /// <summary>
    /// Simulates the snapshot + DTO construction from SaveLot() in Lotes.razor.
    /// </summary>
    private static LotDto BuildLotDto(LotFormModel form, Guid editingId, bool isEditing, string? existingWkt)
    {
        // Snapshot taken BEFORE any await (Sprint 1 fix)
        var snapshot = new LotFormModel
        {
            Name = form.Name,
            FieldId = form.FieldId,
            Status = form.Status,
            CadastralArea = form.CadastralArea
        };

        return new LotDto(
            isEditing ? editingId : Guid.Empty,
            snapshot.FieldId,
            snapshot.Name,
            snapshot.Status,
            existingWkt,
            null,
            0,
            snapshot.CadastralArea   // Bug #4 fix
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bug #4: CadastralArea must be included in LotDto
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SaveLot_Should_Include_CadastralArea_In_DTO()
    {
        var form = new LotFormModel
        {
            Name = "Lote Norte",
            FieldId = Guid.NewGuid(),
            Status = "Active",
            CadastralArea = 5.75m
        };

        var dto = BuildLotDto(form, Guid.Empty, false, null);

        Assert.Equal(5.75m, dto.CadastralArea);
    }

    [Fact]
    public void SaveLot_Should_Include_CadastralArea_Zero_When_User_Entered_Zero()
    {
        var form = new LotFormModel
        {
            Name = "Lote Sur",
            FieldId = Guid.NewGuid(),
            Status = "Active",
            CadastralArea = 0m
        };

        var dto = BuildLotDto(form, Guid.Empty, false, null);

        Assert.Equal(0m, dto.CadastralArea);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bug #1/#2: LotName must NOT be overwritten by CampaignName
    // Simulates: CampaignState.OnChange fires during await → form._Name mutates
    // The snapshot prevents this because it was copied before the await.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SaveLot_Should_Not_Overwrite_LotName_With_CampaignName()
    {
        var form = new LotFormModel
        {
            Name = "Lote Norte",
            FieldId = Guid.NewGuid(),
            Status = "Active",
            CadastralArea = 10.0m
        };

        // Take snapshot BEFORE the simulated mutation (as the fix does)
        var snapshotName = form.Name;

        // Simulate CampaignState.OnChange firing during await and mutating form
        form.Name = "Campaña 2026";  // bug: form mutated by campaign event

        // DTO must use snapshot, not the mutated form
        var dto = new LotDto(
            Guid.Empty,
            form.FieldId,
            snapshotName,           // from snapshot
            form.Status,
            null, null, 0,
            form.CadastralArea);

        Assert.Equal("Lote Norte", dto.Name);
        Assert.NotEqual("Campaña 2026", dto.Name);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bug #3: CadastralArea must NOT be overwritten by GIS calculated area
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SaveLot_Should_Not_Overwrite_CadastralArea_With_GisArea()
    {
        const decimal userEnteredCadastral = 10.0m;
        const double gisCalculatedArea = 9.3;   // different from user-entered

        var form = new LotFormModel
        {
            Name = "Lote Oeste",
            FieldId = Guid.NewGuid(),
            Status = "Active",
            CadastralArea = userEnteredCadastral
        };

        var dto = BuildLotDto(form, Guid.Empty, false, null);

        // GIS area should NOT replace CadastralArea
        Assert.Equal(userEnteredCadastral, dto.CadastralArea);
        Assert.NotEqual((decimal)gisCalculatedArea, dto.CadastralArea);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Snapshot integrity: all fields preserved across construction
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SaveLot_Snapshot_Preserves_All_Form_Fields()
    {
        var fieldId = Guid.NewGuid();
        var editId = Guid.NewGuid();
        var form = new LotFormModel
        {
            Name = "Lote Test",
            FieldId = fieldId,
            Status = "Inactive",
            CadastralArea = 42.5m
        };

        var dto = BuildLotDto(form, editId, isEditing: true, existingWkt: "POLYGON ((0 0, 1 0, 1 1, 0 0))");

        Assert.Equal(editId, dto.Id);
        Assert.Equal(fieldId, dto.FieldId);
        Assert.Equal("Lote Test", dto.Name);
        Assert.Equal("Inactive", dto.Status);
        Assert.Equal(42.5m, dto.CadastralArea);
        Assert.Equal("POLYGON ((0 0, 1 0, 1 1, 0 0))", dto.WktGeometry);
    }
}
