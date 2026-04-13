namespace GestorOT.Application.Interfaces;

public interface ICampaignContextService
{
    Guid? CurrentCampaignId { get; }
}
