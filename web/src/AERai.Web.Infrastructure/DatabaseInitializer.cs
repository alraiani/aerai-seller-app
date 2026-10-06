using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Infrastructure.Identity;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure;

/// <summary>
/// Startup routine that prepares the database: optionally applies migrations, then seeds roles,
/// the default (disabled) ingestion schedules, and the optional first administrator.
/// </summary>
public static partial class DatabaseInitializer
{
    /// <summary>
    /// Runs the initializer in its own DI scope.
    /// </summary>
    /// <param name="services">The application's root service provider.</param>
    /// <param name="applyMigrations">
    /// <see langword="true"/> in Development only. Production schema changes are applied by the
    /// deployment pipeline so that app instances never race to migrate.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="InvalidOperationException">A role or the seed administrator could not be created.</exception>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool applyMigrations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (applyMigrations)
        {
            await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            LogMigrated(logger);
        }

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)).ConfigureAwait(false), $"create role {role}");
            }
        }

        await SeedDefaultSchedulesAsync(provider.GetRequiredService<AppDbContext>(), provider.GetRequiredService<TimeProvider>(), cancellationToken).ConfigureAwait(false);

        var seed = provider.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            return;
        }

        var userManager = provider.GetRequiredService<UserManager<AppUser>>();
        if (await userManager.FindByEmailAsync(seed.AdminEmail).ConfigureAwait(false) is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = seed.AdminEmail,
            Email = seed.AdminEmail,
            DisplayName = "Administrator",
            EmailConfirmed = true,
        };

        EnsureSucceeded(await userManager.CreateAsync(admin, seed.AdminPassword).ConfigureAwait(false), "create the seed administrator");
        EnsureSucceeded(await userManager.AddToRoleAsync(admin, AppRoles.Admin).ConfigureAwait(false), "assign the Admin role");
        LogSeededAdmin(logger, seed.AdminEmail);
    }

    /// <summary>
    /// Creates one schedule per report type for each active marketplace that has never had any, so
    /// the Schedules page starts populated (including for a marketplace activated later). They are
    /// created <b>disabled</b>: nothing calls Amazon until an operator turns a schedule on.
    /// </summary>
    private static async Task SeedDefaultSchedulesAsync(AppDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        // Include deleted schedules: an operator who deleted every default must not get them back.
        var seeded = await db.SyncSchedules.IgnoreQueryFilters()
            .Select(s => s.MarketplaceId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var marketplaces = await db.Marketplaces
            .Where(m => m.IsActive && !seeded.Contains(m.MarketplaceId))
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = clock.GetUtcNow();
        foreach (var m in marketplaces)
        {
            db.SyncSchedules.AddRange(
                DefaultSchedule(m, "Orders — hourly", AmazonReportType.Orders, ScheduleFrequency.Interval, 60, null, 7, now),
                DefaultSchedule(m, "FBA inventory — daily 6:00 AM", AmazonReportType.FbaInventory, ScheduleFrequency.Daily, null, new TimeOnly(6, 0), 1, now),
                DefaultSchedule(m, "Settlements — daily 7:00 AM", AmazonReportType.Settlements, ScheduleFrequency.Daily, null, new TimeOnly(7, 0), 30, now));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SyncSchedule DefaultSchedule(
        Marketplace marketplace, string name, AmazonReportType type, ScheduleFrequency frequency, int? intervalMinutes, TimeOnly? dailyTime, int lookbackDays, DateTimeOffset now) => new()
    {
        // Names are unique across marketplaces, so each default carries its marketplace code.
        Name = $"{marketplace.Code} {name}",
        ReportType = type,
        MarketplaceId = marketplace.MarketplaceId,
        IsEnabled = false,
        Frequency = frequency,
        IntervalMinutes = intervalMinutes,
        DailyTime = dailyTime,
        TimeZoneId = marketplace.TimeZoneId,
        LookbackDays = lookbackDays,
        AutoPromote = true,
        CreatedAt = now,
        UpdatedAt = now,
        UpdatedBy = "system",
    };

    /// <summary>Turns a failed Identity result into a startup failure with a readable message.</summary>
    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Database initialization could not {action}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied pending database migrations")]
    private static partial void LogMigrated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded administrator account {Email}")]
    private static partial void LogSeededAdmin(ILogger logger, string email);
}
