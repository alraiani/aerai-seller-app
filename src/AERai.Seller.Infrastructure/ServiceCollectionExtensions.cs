using AERai.Seller.Application.Abstractions;
using AERai.Seller.Infrastructure.Repositories;
using AERai.Seller.SpApiClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Seller.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string sqliteDbPath)
    {
        // IDbContextFactory (not AddDbContext) is Microsoft's recommended EF Core pattern for WPF/desktop
        // apps: repositories create a short-lived DbContext per call instead of holding a long-lived one,
        // so they (and everything above them) can safely be registered as Singleton.
        services.AddDbContextFactory<SellerDbContext>(options => options.UseSqlite($"Data Source={sqliteDbPath}"));

        services.AddSingleton<DpapiSettingsStore>(_ => new DpapiSettingsStore());
        services.AddSingleton<IAppSettingsStore>(sp => sp.GetRequiredService<DpapiSettingsStore>());
        services.AddSingleton<ICredentialStore>(sp => sp.GetRequiredService<DpapiSettingsStore>());

        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IInventoryRepository, InventoryRepository>();
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<ICatalogRepository, CatalogRepository>();
        services.AddSingleton<ISyncMetadataRepository, SyncMetadataRepository>();
        services.AddSingleton<IAwdInventoryRepository, AwdInventoryRepository>();
        services.AddSingleton<ILeadTimeProfileRepository, LeadTimeProfileRepository>();
        services.AddSingleton<IDemandForecastRepository, DemandForecastRepository>();
        services.AddSingleton<IReplenishmentRecommendationRepository, ReplenishmentRecommendationRepository>();
        services.AddSingleton<ISettlementRepository, SettlementRepository>();
        services.AddSingleton<IBookkeepingAccountMappingRepository, BookkeepingAccountMappingRepository>();
        services.AddSingleton<IBookkeepingSettingsRepository, BookkeepingSettingsRepository>();
        services.AddSingleton<IBookkeepingExportRepository, BookkeepingExportRepository>();
        services.AddSingleton<IExportFileStore, FileExportStore>();

        return services;
    }

    public static string GetDefaultSqliteDbPath()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AERaiSellerApp");
        Directory.CreateDirectory(appDataDir);
        return Path.Combine(appDataDir, "aerai-seller.db");
    }
}
