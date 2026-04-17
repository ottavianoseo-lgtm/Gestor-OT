using GestorOT.Shared.Dtos;
using Microsoft.JSInterop;

namespace GestorOT.Client.Services;

public class TenantState
{
    private readonly IJSRuntime _jsRuntime;
    
    public TenantDto? CurrentTenant { get; private set; }
    public List<TenantDto> AvailableTenants { get; set; } = new();
    public bool IsSelected => CurrentTenant != null;

    public TenantState(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        var tenantId = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "selected_tenant_id");
        // Nota: Esta inicialización debe llamarse desde un componente (ej. MainLayout)
        // Por ahora lo dejamos simple.
    }

    public void SetTenant(TenantDto tenant)
    {
        CurrentTenant = tenant;
        _jsRuntime.InvokeVoidAsync("localStorage.setItem", "selected_tenant_id", tenant.Id.ToString());
        OnChange?.Invoke();
    }

    public void Clear()
    {
        CurrentTenant = null;
        _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "selected_tenant_id");
        OnChange?.Invoke();
    }
}
