using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// OT-48, matching de polígonos importados contra lotes existentes.
///
/// Lo crítico es que el cruce esté acotado al campo destino: los .dbf reales traen el lote como
/// "1", "2", "3" —números sin prefijo del establecimiento—, así que un match global colisionaría
/// entre campos y vincularía la geometría de un campo a un lote de otro.
///
/// Estos tests cubren la propuesta (ProposeAsync), que no toca PostGIS. La aplicación en bloque
/// necesita base real y se verifica aparte.
/// </summary>
public class LotBulkLinkTests
{
    private ApplicationDbContext CreateContext(string dbName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth"))
        };

        return new ApplicationDbContext(options, new HttpContextAccessor { HttpContext = httpContext });
    }

    private sealed record Escenario(string DbName, Guid TenantId, Guid CampoA, Guid CampoB);

    /// <summary>Dos campos con lotes que se llaman igual: "1" y "2" en ambos.</summary>
    private Escenario SeedDosCamposConLotesHomonimos()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        using var ctx = CreateContext(dbName, tenantId);

        var campoA = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "La Celina" };
        var campoB = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "El Alba" };
        ctx.Fields.AddRange(campoA, campoB);

        ctx.Lots.AddRange(
            new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = campoA.Id, Name = "1" },
            new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = campoA.Id, Name = "2" },
            new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = campoB.Id, Name = "1" },
            new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = campoB.Id, Name = "2" });

        ctx.SaveChanges();
        return new Escenario(dbName, tenantId, campoA.Id, campoB.Id);
    }

    private LotBulkLinkService CreateService(ApplicationDbContext ctx) =>
        new(ctx, new LotQueryService(ctx), NullLogger<LotBulkLinkService>.Instance);

    private static ShapefileFeatureDto Feature(string name) =>
        new(name, "POLYGON((0 0,1 0,1 1,0 1,0 0))", 10, new Dictionary<string, string>());

    [Fact]
    public async Task ElMatchSeAcotaAlCampoDestino_YNoColisionaConOtro()
    {
        // Es la regresión que importa: "1" existe en los dos campos. Si el cruce fuera global,
        // sería ambiguo o apuntaría al lote equivocado.
        var e = SeedDosCamposConLotesHomonimos();
        using var ctx = CreateContext(e.DbName, e.TenantId);

        var result = await CreateService(ctx).ProposeAsync(
            new LotMatchRequestDto(e.CampoA, new List<ShapefileFeatureDto> { Feature("1"), Feature("2") }));

        Assert.Equal(2, result.ToLink);
        Assert.Equal(0, result.Ambiguous);
        Assert.All(result.Proposals, p => Assert.Equal(LotMatchStatus.ExactMatch, p.Status));

        // Y cada uno apunta a un lote del campo A, no del B.
        var lotesDeA = await ctx.Lots.Where(l => l.FieldId == e.CampoA).Select(l => l.Id).ToListAsync();
        Assert.All(result.Proposals, p => Assert.Contains(p.MatchedLotId!.Value, lotesDeA));
    }

    [Fact]
    public async Task SinMatch_ProponeCrearElLote()
    {
        var e = SeedDosCamposConLotesHomonimos();
        using var ctx = CreateContext(e.DbName, e.TenantId);

        var result = await CreateService(ctx).ProposeAsync(
            new LotMatchRequestDto(e.CampoA, new List<ShapefileFeatureDto> { Feature("99") }));

        var p = Assert.Single(result.Proposals);
        Assert.Equal(LotMatchStatus.NoMatch, p.Status);
        Assert.Equal(LotLinkAction.Create, p.SuggestedAction);
        Assert.Equal(1, result.ToCreate);
    }

    [Fact]
    public async Task ConVariosCandidatos_QuedaAmbiguoYNoSeAutoVincula()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campo = Guid.NewGuid();

        using (var seed = CreateContext(dbName, tenantId))
        {
            seed.Fields.Add(new Field { Id = campo, TenantId = tenantId, Name = "Campo" });
            // Dos lotes con el mismo nombre dentro del MISMO campo.
            seed.Lots.AddRange(
                new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = campo, Name = "Norte" },
                new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = campo, Name = "Norte" });
            seed.SaveChanges();
        }

        using var ctx = CreateContext(dbName, tenantId);
        var result = await CreateService(ctx).ProposeAsync(
            new LotMatchRequestDto(campo, new List<ShapefileFeatureDto> { Feature("Norte") }));

        var p = Assert.Single(result.Proposals);
        Assert.Equal(LotMatchStatus.Ambiguous, p.Status);
        Assert.Equal(LotLinkAction.Skip, p.SuggestedAction);
        Assert.Null(p.MatchedLotId);
        Assert.Equal(2, p.Candidates.Count);
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData("01", "1")]
    [InlineData(" 1 ", "1")]
    [InlineData("lote norte", "Lote Norte")]
    [InlineData("Lote  Norte", "Lote Norte")]
    public async Task LosNombresSeComparanNormalizados(string enElShapefile, string enElLote)
    {
        // El .dbf suele traer "01" donde el lote se llama "1", o espacios de más.
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campo = Guid.NewGuid();

        using (var seed = CreateContext(dbName, tenantId))
        {
            seed.Fields.Add(new Field { Id = campo, TenantId = tenantId, Name = "Campo" });
            seed.Lots.Add(new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = campo, Name = enElLote });
            seed.SaveChanges();
        }

        using var ctx = CreateContext(dbName, tenantId);
        var result = await CreateService(ctx).ProposeAsync(
            new LotMatchRequestDto(campo, new List<ShapefileFeatureDto> { Feature(enElShapefile) }));

        var p = Assert.Single(result.Proposals);
        Assert.Equal(LotMatchStatus.ExactMatch, p.Status);
        Assert.Equal(enElLote, p.MatchedLotName);
    }

    [Fact]
    public async Task SeAvisaCuandoElLoteDestinoYaTieneGeometria()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campo = Guid.NewGuid();

        using (var seed = CreateContext(dbName, tenantId))
        {
            seed.Fields.Add(new Field { Id = campo, TenantId = tenantId, Name = "Campo" });
            var factory = new NetTopologySuite.Geometries.GeometryFactory();
            seed.Lots.Add(new Lot
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FieldId = campo,
                Name = "1",
                Geometry = factory.CreatePolygon(new[]
                {
                    new NetTopologySuite.Geometries.Coordinate(0, 0),
                    new NetTopologySuite.Geometries.Coordinate(1, 0),
                    new NetTopologySuite.Geometries.Coordinate(1, 1),
                    new NetTopologySuite.Geometries.Coordinate(0, 0)
                })
            });
            seed.SaveChanges();
        }

        using var ctx = CreateContext(dbName, tenantId);
        var result = await CreateService(ctx).ProposeAsync(
            new LotMatchRequestDto(campo, new List<ShapefileFeatureDto> { Feature("1") }));

        var p = Assert.Single(result.Proposals);
        Assert.True(p.TargetHasGeometry, "Hay que avisar que se va a pisar una geometría existente.");
    }

    [Fact]
    public async Task FeatureSinNombre_NoProponeCrearUnLoteSinNombre()
    {
        var e = SeedDosCamposConLotesHomonimos();
        using var ctx = CreateContext(e.DbName, e.TenantId);

        var result = await CreateService(ctx).ProposeAsync(
            new LotMatchRequestDto(e.CampoA, new List<ShapefileFeatureDto> { Feature("") }));

        var p = Assert.Single(result.Proposals);
        Assert.Equal(LotLinkAction.Skip, p.SuggestedAction);
        Assert.Equal(0, result.ToCreate);
    }
}
