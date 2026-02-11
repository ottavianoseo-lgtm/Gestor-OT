using GestorOT.Shared.Dtos;

namespace GestorOT.Client.Services;

public class TenantState
{
    public TenantDto? CurrentTenant { get; private set; }
    public List<TenantDto> AvailableTenants { get; set; } = new();
    public bool IsSelected => CurrentTenant != null;

    public event Action? OnChange;

    public void SetTenant(TenantDto tenant)
    {
        CurrentTenant = tenant;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        CurrentTenant = null;
        OnChange?.Invoke();
    }
}
