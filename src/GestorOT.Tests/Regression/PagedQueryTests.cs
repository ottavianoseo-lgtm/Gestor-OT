using System.ComponentModel.DataAnnotations;
using GestorOT.Api.Extensions;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// Contrato de los GET .../search que usa el MCP: página obligatoria, tope de 100, orden solo
/// por claves declaradas y páginas estables.
/// </summary>
public class PagedQueryTests
{
    private static readonly SortMap<Field> Sorts = new SortMap<Field>(f => f.Id)
        .Add("name", f => f.Name)
        .Add("codCentro", f => f.CodCentro, defaultDesc: true);

    private static ApplicationDbContext CreateContext(int fields)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        for (var i = 1; i <= fields; i++)
            context.Fields.Add(new Field { Id = Guid.NewGuid(), Name = $"Campo {i:D2}", CodCentro = i });
        context.SaveChanges();
        return context;
    }

    private static async Task<PagedResult<string>> Run(ApplicationDbContext context, PagedQuery paging)
    {
        var result = await context.Fields.AsNoTracking().ToPagedAsync(paging, Sorts, f => f.Name, CancellationToken.None);
        return Assert.IsType<PagedResult<string>>(result.Value);
    }

    [Fact]
    public async Task Corta_la_pagina_y_devuelve_el_total()
    {
        using var context = CreateContext(30);

        var page2 = await Run(context, new PagedQuery { Page = 2, PageSize = 10 });

        Assert.Equal(30, page2.Total);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal("Campo 11", page2.Items[0]);
        Assert.True(page2.HasNext);
    }

    [Fact]
    public async Task Sin_sortBy_usa_la_primera_clave_y_respeta_su_direccion_por_defecto()
    {
        using var context = CreateContext(5);

        var byName = await Run(context, new PagedQuery { Page = 1, PageSize = 5 });
        var byCodDesc = await Run(context, new PagedQuery { Page = 1, PageSize = 5, SortBy = "CODCENTRO" });
        var byCodAsc = await Run(context, new PagedQuery { Page = 1, PageSize = 5, SortBy = "codCentro", SortDir = "asc" });

        Assert.Equal("Campo 01", byName.Items[0]);
        Assert.Equal("Campo 05", byCodDesc.Items[0]);
        Assert.Equal("Campo 01", byCodAsc.Items[0]);
    }

    [Theory]
    [InlineData("geometry", null)]
    [InlineData("name", "sideways")]
    public async Task Orden_invalido_devuelve_400_con_las_opciones(string sortBy, string? sortDir)
    {
        using var context = CreateContext(3);

        var result = await context.Fields.ToPagedAsync(
            new PagedQuery { Page = 1, PageSize = 10, SortBy = sortBy, SortDir = sortDir }, Sorts, f => f.Name, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(bad.Value);
        Assert.Contains(sortDir is null ? "name, codCentro" : "asc o desc", problem.Title);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(1, null)]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Pagina_faltante_o_fuera_de_rango_no_valida(int? page, int? pageSize)
    {
        var paging = new PagedQuery { Page = page, PageSize = pageSize };

        var valid = Validator.TryValidateObject(paging, new ValidationContext(paging), new List<ValidationResult>(), validateAllProperties: true);

        Assert.False(valid);
    }

    [Fact]
    public void Patron_de_busqueda_escapa_comodines()
    {
        Assert.Equal(@"%10\%\_a%", PagedQueryExtensions.ContainsPattern(" 10%_a "));
    }
}
