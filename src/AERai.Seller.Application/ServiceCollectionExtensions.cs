using AERai.Seller.Application.Bookkeeping;
using AERai.Seller.Application.Dashboard;
using AERai.Seller.Application.Replenishment;
using AERai.Seller.Application.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Seller.Application;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Application-layer services. Requires Infrastructure to already have registered
    /// implementations of the repository interfaces in AERai.Seller.Application.Abstractions.
    /// Safe to register as Singleton: repositories use IDbContextFactory internally (Microsoft's
    /// recommended EF Core pattern for WPF/desktop apps) rather than holding a long-lived DbContext,
    /// so there's no ASP.NET-style request-scope needed here.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInventorySyncService, InventorySyncService>();
        services.AddSingleton<IOrderSyncService, OrderSyncService>();
        services.AddSingleton<ICatalogSyncService, CatalogSyncService>();
        services.AddSingleton<IAwdInventorySyncService, AwdInventorySyncService>();
        services.AddSingleton<ISettlementSyncService, SettlementSyncService>();
        services.AddSingleton<IDashboardQueryService, DashboardQueryService>();
        services.AddSingleton<IReplenishmentPlanningService, ReplenishmentPlanningService>();
        services.AddSingleton<IReplenishmentQueryService, ReplenishmentQueryService>();
        services.AddSingleton<IBookkeepingExportService, BookkeepingExportService>();

        return services;
    }
}
