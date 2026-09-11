using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace GestorOT.Infrastructure.Data;

/// <summary>
/// Sobre qué empresa corre una request. Lo consultan el filtro global de EF y el interceptor
/// que setea app.current_tenant, y tienen que responder lo mismo: si cada uno resuelve el
/// tenant por su cuenta, los filtros de la app y las políticas de la base dejan de coincidir.
/// </summary>
public readonly record struct TenantScope(Guid TenantId, bool CrossTenant)
{
    /// <summary>Sin tenant y sin permiso de cruzar: las consultas no devuelven nada.</summary>
    public static readonly TenantScope Nothing = new(Guid.Empty, false);

    /// <summary>Fuera de una request (migraciones, jobs, tests) no hay a quién filtrar.</summary>
    public static readonly TenantScope Unrestricted = new(Guid.Empty, true);

    public static TenantScope Resolve(HttpContext? httpContext)
    {
        if (httpContext == null) return Unrestricted;

        var user = httpContext.User;
        var isSuperAdmin = user?.IsInRole("SuperAdmin") == true
                           || user?.FindFirst(ClaimTypes.Role)?.Value == "SuperAdmin";

        if (isSuperAdmin)
        {
            // El SuperAdmin elige sobre qué empresa trabaja y lo manda en el header; si no
            // eligió ninguna queda en Global, que es el único caso donde ver todas es correcto.
            var header = httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();
            return Guid.TryParse(header, out var headerTenantId) && headerTenantId != Guid.Empty
                ? new TenantScope(headerTenantId, false)
                : Unrestricted;
        }

        // Para el resto el tenant sale del token y de ningún otro lado. El header lo escribe
        // el cliente, así que aceptarlo acá sería dejar que cualquiera elija empresa.
        var claimTenant = user?.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(claimTenant, out var userTenantId) && userTenantId != Guid.Empty)
        {
            return new TenantScope(userTenantId, false);
        }

        // Anónimo, o token sin tenant: no se ve nada. Antes esto caía en "ver todo", que es
        // como un descuido de sesión terminaba mostrando datos de otra empresa. Los flujos
        // públicos (links compartidos, login) no dependen de esto: piden IgnoreQueryFilters().
        return Nothing;
    }
}
