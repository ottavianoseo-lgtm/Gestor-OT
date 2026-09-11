using GestorOT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestorOT.Api.Extensions;

public static class MigrationExtensions
{
    private const int MaxAttempts = 20;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Applies pending EF Core migrations at startup in all environments.
    /// Retries with exponential backoff while the database is still starting up
    /// (e.g. recovering after a host reboot), and throws if the database is
    /// unreachable or a non-transient migration error occurs.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<ApplicationDbContext>>();

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var delay = InitialDelay;

        for (var attempt = 1; ; attempt++)
        {
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
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                logger.LogWarning(
                    ex,
                    "Database not ready (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    attempt,
                    MaxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, MaxDelay.TotalSeconds));
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

    private static bool IsTransient(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException npgsql && npgsql.IsTransient)
            {
                return true;
            }

            if (current is PostgresException { SqlState: "57P03" })
            {
                return true;
            }
        }

        return false;
    }
}
