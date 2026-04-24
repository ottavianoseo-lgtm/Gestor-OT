using GestorOT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations at startup.
    /// Only runs in Development and Staging environments.
    /// In Production the application starts normally and migrations must be applied
    /// via the external script (see docs/migrations.md).
    /// Throws and halts startup if the database is unreachable or migration fails.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() && !app.Environment.IsStaging())
            return;

        var logger = app.Services.GetRequiredService<ILogger<ApplicationDbContext>>();

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var pending = await context.Database.GetPendingMigrationsAsync();
            var pendingList = pending.ToList();

            if (pendingList.Count == 0)
            {
                logger.LogInformation("Database schema is up to date — no pending migrations.");
                return;
            }

            logger.LogInformation(
                "Applying {Count} pending migration(s): {Names}",
                pendingList.Count,
                string.Join(", ", pendingList));

            await context.Database.MigrateAsync();

            logger.LogInformation("All migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "Database migration failed. The application cannot start with an out-of-sync schema.");

            // Re-throw so the host exits with a non-zero code and the error is visible
            throw;
        }
    }
}
