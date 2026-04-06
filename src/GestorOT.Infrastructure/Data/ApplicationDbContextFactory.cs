using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace GestorOT.Infrastructure.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Prioridad 1: SUPABASE_MIGRATION_CONNECTION_STRING (Entorno directo/sesion para migraciones)
        // Prioridad 2: SUPABASE_CONNECTION_STRING (La de la App)
        // Prioridad 3: appsettings.json
        
        string? connectionString = Environment.GetEnvironmentVariable("SUPABASE_MIGRATION_CONNECTION_STRING") 
                                 ?? Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("No se encontro la cadena de conexión para migraciones. Configure SUPABASE_MIGRATION_CONNECTION_STRING.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o =>
        {
            o.UseNetTopologySuite();
            o.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            o.CommandTimeout(300);
        });

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
