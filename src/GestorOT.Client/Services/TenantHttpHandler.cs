namespace GestorOT.Client.Services;

public class TenantHttpHandler : DelegatingHandler
{
    private readonly TenantState _tenantState;

    public TenantHttpHandler(TenantState tenantState)
    {
        _tenantState = tenantState;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Tenant-ID");
        if (_tenantState.CurrentTenant != null && _tenantState.CurrentTenant.Id != Guid.Empty)
        {
            request.Headers.Add("X-Tenant-ID", _tenantState.CurrentTenant.Id.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
