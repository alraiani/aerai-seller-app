using AERai.Web.Application.Alerts;
using AERai.Web.Application.Dashboard;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Products;
using AERai.Web.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Application;

/// <summary>
/// Registers Application-layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds use-case services, parsers, mappers, and validated options. Infrastructure must also be
    /// registered (via <c>AddInfrastructure</c>) to supply the abstractions these services depend on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ImportOptions>()
            .BindConfiguration(ImportOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DashboardOptions>()
            .BindConfiguration(DashboardOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<InventoryOptions>()
            .BindConfiguration(InventoryOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IngestionOptions>()
            .BindConfiguration(IngestionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IStagingRowMapper, OrderLineMapper>();
        services.AddSingleton<IStagingRowMapper, InventoryRowMapper>();
        services.AddSingleton<IStagingRowMapper, SettlementLineMapper>();
        services.AddSingleton<IStagingRowMapper, FbaInventoryRowMapper>();
        services.AddSingleton<IStagingRowMapper, FbaReservedInventoryRowMapper>();

        // Scoped because they depend on scoped Infrastructure services (DbContext-backed).
        services.AddScoped<IStagingImportService, StagingImportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IInventoryItemService, InventoryItemService>();
        services.AddScoped<IProductFamilyService, ProductFamilyService>();
        services.AddScoped<IStockAlertService, StockAlertService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICurrentMarketplace, CurrentMarketplace>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IReportIngestionService, ReportIngestionService>();
        services.AddScoped<ISyncScheduleService, SyncScheduleService>();

        return services;
    }
}
