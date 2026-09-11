using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// Aislamiento entre empresas.
///
/// El bug que motivó estos tests: la página de Tipo de Labores le mostraba al SuperAdmin los
/// conceptos de todas las empresas aunque hubiera elegido una. La causa era que "no sé de qué
/// tenant es esta request" y "mostrame todas" eran el mismo valor (Guid.Empty), así que
/// cualquier request que llegara sin X-Tenant-ID abría el filtro en vez de cerrarlo.
///
/// La regla que se fija acá: solo el SuperAdmin sin empresa elegida ve más de una.
/// </summary>
public class TenantIsolationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ApplicationDbContext CreateContext(
        string dbName,
        string? role,
        Guid? tenantClaim,
        string? tenantHeader = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var claims = new List<Claim>();
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (tenantClaim != null) claims.Add(new Claim("tenant_id", tenantClaim.Value.ToString()));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };

        if (tenantHeader != null)
        {
            httpContext.Request.Headers["X-Tenant-ID"] = tenantHeader;
        }

        return new ApplicationDbContext(options, new HttpContextAccessor { HttpContext = httpContext });
    }

    /// <summary>Un concepto de cada empresa, que es lo mínimo para notar una fuga.</summary>
    private static string SeedUnConceptoPorEmpresa()
    {
        var dbName = Guid.NewGuid().ToString();

        // Sin HttpContext el contexto no filtra, que es lo que hace falta para poder sembrar.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var seed = new ApplicationDbContext(options);
        seed.ErpConcepts.AddRange(
            new ErpConcept { Id = Guid.NewGuid(), TenantId = TenantA, Description = "Siembra A", GrupoConcepto = "LABOR" },
            new ErpConcept { Id = Guid.NewGuid(), TenantId = TenantB, Description = "Siembra B", GrupoConcepto = "LABOR" });
        seed.SaveChanges();

        return dbName;
    }

    [Fact]
    public async Task SuperAdminConEmpresaElegida_SoloVeEsaEmpresa()
    {
        // La regresión exacta que reportó el usuario.
        var db = SeedUnConceptoPorEmpresa();
        using var ctx = CreateContext(db, "SuperAdmin", null, TenantA.ToString());

        var conceptos = await ctx.ErpConcepts.ToListAsync();

        var unico = Assert.Single(conceptos);
        Assert.Equal(TenantA, unico.TenantId);
    }

    [Fact]
    public async Task SuperAdminSinElegirEmpresa_VeTodas()
    {
        // El modo Global es una función buscada del selector, no un accidente: se conserva.
        var db = SeedUnConceptoPorEmpresa();
        using var ctx = CreateContext(db, "SuperAdmin", null);

        Assert.Equal(2, await ctx.ErpConcepts.CountAsync());
    }

    [Fact]
    public async Task UsuarioNormal_SoloVeLaEmpresaDeSuToken()
    {
        var db = SeedUnConceptoPorEmpresa();
        using var ctx = CreateContext(db, "Admin", TenantA);

        var unico = Assert.Single(await ctx.ErpConcepts.ToListAsync());
        Assert.Equal(TenantA, unico.TenantId);
    }

    [Fact]
    public async Task UsuarioNormal_NoPuedeCambiarDeEmpresaMandandoElHeader()
    {
        // El header lo escribe el cliente, así que para cualquiera que no sea SuperAdmin
        // manda el token y el header se ignora.
        var db = SeedUnConceptoPorEmpresa();
        using var ctx = CreateContext(db, "Admin", TenantA, TenantB.ToString());

        var unico = Assert.Single(await ctx.ErpConcepts.ToListAsync());
        Assert.Equal(TenantA, unico.TenantId);
    }

    [Fact]
    public async Task SinTenantYSinSerSuperAdmin_NoVeNada()
    {
        // Acá está el corazón del bug: antes esto devolvía las dos empresas.
        var db = SeedUnConceptoPorEmpresa();
        using var ctx = CreateContext(db, "Admin", tenantClaim: null);

        Assert.Empty(await ctx.ErpConcepts.ToListAsync());
    }

    [Fact]
    public async Task AnonimoConHeaderArmadoAMano_NoVeNada()
    {
        var db = SeedUnConceptoPorEmpresa();
        using var ctx = CreateContext(db, role: null, tenantClaim: null, tenantHeader: TenantA.ToString());

        Assert.Empty(await ctx.ErpConcepts.ToListAsync());
    }

    [Fact]
    public async Task BuscarPorIdUnRegistroDeOtraEmpresa_NoLoEncuentra()
    {
        // Vale tanto para FirstOrDefaultAsync como para FindAsync. Lo segundo no es obvio:
        // Find resuelve por clave y durante varias versiones de EF se salteaba los filtros
        // globales (de ahí el comentario que quedó en LotsController). En EF 10 ya no, y se
        // deja medido acá para enterarnos si vuelve a cambiar: hay una treintena de
        // búsquedas por id en la API que dependen de esto para no cruzar empresas.
        var db = SeedUnConceptoPorEmpresa();

        Guid idDeB;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(db).Options;
        using (var sinFiltro = new ApplicationDbContext(options))
        {
            idDeB = await sinFiltro.ErpConcepts.Where(c => c.TenantId == TenantB).Select(c => c.Id).SingleAsync();
        }

        using var ctx = CreateContext(db, "Admin", TenantA);

        Assert.Null(await ctx.ErpConcepts.FirstOrDefaultAsync(c => c.Id == idDeB));
        Assert.Null(await ctx.ErpConcepts.FindAsync(idDeB));
    }

    [Fact]
    public async Task LaborTypes_TambienQuedanAcotadosAlTenant()
    {
        // La página cruza conceptos contra LaborTypes para marcar cuáles están activados:
        // si el cruce se escapa de la empresa, marca como activado lo de otra.
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;

        using (var seed = new ApplicationDbContext(options))
        {
            seed.LaborTypes.AddRange(
                new LaborType { Id = Guid.NewGuid(), TenantId = TenantA, Name = "Siembra", ExternalErpId = "100" },
                new LaborType { Id = Guid.NewGuid(), TenantId = TenantB, Name = "Siembra", ExternalErpId = "200" });
            seed.SaveChanges();
        }

        using var ctx = CreateContext(dbName, "SuperAdmin", null, TenantA.ToString());

        var activados = await ctx.LaborTypes.Select(l => l.ExternalErpId).ToListAsync();

        Assert.Equal(new[] { "100" }, activados);
    }
}
