using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class RotationService : IRotationService
{
    private readonly IApplicationDbContext _context;

    public RotationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RotationDto>> GetRotationsByCampaignLotAsync(Guid campaignLotId, CancellationToken ct = default)
    {
        return await _context.Rotations
            .AsNoTracking()
            .Where(r => r.CampaignLotId == campaignLotId)
            .Include(r => r.SuggestedLaborType)
            .OrderBy(r => r.StartDate)
            .Select(r => new RotationDto(
                r.Id,
                r.CampaignLotId,
                r.CropName,
                r.StartDate,
                r.EndDate,
                r.Notes,
                r.SuggestedLaborTypeId,
                r.SuggestedLaborType != null ? r.SuggestedLaborType.Name : null
            ))
            .ToListAsync(ct);
    }

    public async Task<RotationDto?> GetActiveRotationAsync(Guid campaignLotId, DateOnly date, CancellationToken ct = default)
    {
        return await _context.Rotations
            .AsNoTracking()
            .Where(r => r.CampaignLotId == campaignLotId && r.StartDate <= date && r.EndDate >= date)
            .Include(r => r.SuggestedLaborType)
            .Select(r => new RotationDto(
                r.Id,
                r.CampaignLotId,
                r.CropName,
                r.StartDate,
                r.EndDate,
                r.Notes,
                r.SuggestedLaborTypeId,
                r.SuggestedLaborType != null ? r.SuggestedLaborType.Name : null
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RotationResponse> CreateRotationAsync(RotationDto dto, CancellationToken ct = default)
    {
        var rotation = new Rotation
        {
            Id = Guid.NewGuid(),
            CampaignLotId = dto.CampaignLotId,
            CropName = dto.CropName,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Notes = dto.Notes,
            SuggestedLaborTypeId = dto.SuggestedLaborTypeId
        };

        _context.Rotations.Add(rotation);
        await _context.SaveChangesAsync(ct);

        var warnings = await GetRotationWarningsAsync(rotation, ct);

        return new RotationResponse(
            new RotationDto(
                rotation.Id,
                rotation.CampaignLotId,
                rotation.CropName,
                rotation.StartDate,
                rotation.EndDate,
                rotation.Notes,
                rotation.SuggestedLaborTypeId
            ),
            warnings
        );
    }

    public async Task UpdateRotationAsync(Guid id, RotationDto dto, CancellationToken ct = default)
    {
        var rotation = await _context.Rotations.FindAsync(new object[] { id }, ct);
        if (rotation == null) return;

        rotation.CropName = dto.CropName;
        rotation.StartDate = dto.StartDate;
        rotation.EndDate = dto.EndDate;
        rotation.Notes = dto.Notes;
        rotation.SuggestedLaborTypeId = dto.SuggestedLaborTypeId;

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteRotationAsync(Guid id, CancellationToken ct = default)
    {
        var rotation = await _context.Rotations.FindAsync(new object[] { id }, ct);
        if (rotation == null) return;

        _context.Rotations.Remove(rotation);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<RotationWarning>> ValidateRotationEndDatesAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await _context.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);

        if (campaign == null || campaign.EndDate == null) return new List<RotationWarning>();

        var campaignEndDate = campaign.EndDate.Value;

        return await _context.Rotations
            .AsNoTracking()
            .Include(r => r.CampaignLot)
                .ThenInclude(cl => cl!.Lot)
            .Where(r => r.CampaignLot!.CampaignId == campaignId && r.EndDate > campaignEndDate)
            .Select(r => new RotationWarning(
                r.CampaignLot!.Lot!.Name,
                r.EndDate,
                campaignEndDate,
                "La rotación supera el cierre de la campaña."
            ))
            .ToListAsync(ct);
    }

    private async Task<List<RotationWarning>> GetRotationWarningsAsync(Rotation rotation, CancellationToken ct)
    {
        var warnings = new List<RotationWarning>();

        var campaignData = await _context.CampaignLots
            .AsNoTracking()
            .Include(cl => cl.Campaign)
            .Include(cl => cl.Lot)
            .FirstOrDefaultAsync(cl => cl.Id == rotation.CampaignLotId, ct);

        if (campaignData?.Campaign?.EndDate != null && rotation.EndDate > campaignData.Campaign.EndDate.Value)
        {
            warnings.Add(new RotationWarning(
                campaignData.Lot?.Name ?? "Lote desconocido",
                rotation.EndDate,
                campaignData.Campaign.EndDate.Value,
                "La rotación supera el cierre de la campaña."
            ));
        }

        return warnings;
    }
}
