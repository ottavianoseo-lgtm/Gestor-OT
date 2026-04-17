using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GestorOT.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Field> Fields { get; }
    DbSet<Lot> Lots { get; }
    DbSet<WorkOrder> WorkOrders { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<Labor> Labors { get; }
    DbSet<LaborSupply> LaborSupplies { get; }
    DbSet<CropStrategy> CropStrategies { get; }
    DbSet<StrategyItem> StrategyItems { get; }
    DbSet<LaborType> LaborTypes { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<SharedToken> SharedTokens { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<TankMixRule> TankMixRules { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<CampaignLot> CampaignLots { get; }
    DbSet<Rotation> Rotations { get; }
    DbSet<CampaignField> CampaignFields { get; }
    DbSet<ErpPerson> ErpPeople { get; }
    DbSet<ErpActivity> ErpActivities { get; }
    DbSet<ErpConcept> ErpConcepts { get; }
    DbSet<WorkOrderStatus> WorkOrderStatuses { get; }
    DbSet<LaborAttachment> LaborAttachments { get; }
    DbSet<WorkOrderSupplyApproval> WorkOrderSupplyApprovals { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
