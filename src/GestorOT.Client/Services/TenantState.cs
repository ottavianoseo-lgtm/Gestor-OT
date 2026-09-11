using GestorOT.Shared.Dtos;
using Microsoft.JSInterop;

namespace GestorOT.Client.Services;

public class TenantState
{
    private const string StorageKeyId = "selected_tenant_id";
    private const string StorageKeyName = "selected_tenant_name";

    private readonly IJSRuntime _jsRuntime;
    
    public TenantDto? CurrentTenant { get; private set; }
    public List<TenantDto> AvailableTenants { get; set; } = new();
    public bool IsSelected => CurrentTenant != null && CurrentTenant.Id != Guid.Empty;
    public bool IsGlobalMode => CurrentTenant == null || CurrentTenant.Id == Guid.Empty;

    public TenantState(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? OnChange;

    /// <summary>
    /// Recupera la empresa elegida de una sesión anterior. Se llama al arrancar, antes de que
    /// se renderice nada: si una página consulta la API sin este dato, el backend no sabe sobre
    /// qué empresa está trabajando el SuperAdmin y le contesta con las de todas.
    /// </summary>
    public async Task RestoreAsync()
    {
        try
        {
            var tenantId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKeyId);
            if (!Guid.TryParse(tenantId, out var id) || id == Guid.Empty) return;

            var name = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKeyName);

            // Se asigna directo y no por SetTenant: no hay nada nuevo que guardar.
            CurrentTenant = new TenantDto(id, name ?? "Empresa", null, null, DateTime.MinValue);
            OnChange?.Invoke();
        }
        catch
        {
            // Sin localStorage (modo privado, storage bloqueado) se arranca sin empresa elegida.
        }
    }

    public void SetTenant(TenantDto tenant)
    {
        CurrentTenant = tenant;
        if (tenant.Id != Guid.Empty)
        {
            _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKeyId, tenant.Id.ToString());
            _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKeyName, tenant.Name);
        }
        else
        {
            Forget();
        }
        OnChange?.Invoke();
    }

    public void SetGlobal()
    {
        CurrentTenant = new TenantDto(Guid.Empty, "Global / Todas las Empresas", null, null, DateTime.MinValue);
        Forget();
        OnChange?.Invoke();
    }

    public void Clear()
    {
        CurrentTenant = null;
        Forget();
        OnChange?.Invoke();
    }

    private void Forget()
    {
        _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKeyId);
        _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKeyName);
    }
}
