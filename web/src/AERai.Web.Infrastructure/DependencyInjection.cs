using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Security;
using AERai.Web.Infrastructure.Alerts;
using AERai.Web.Infrastructure.Email;
using AERai.Web.Infrastructure.Identity;
using AERai.Web.Infrastructure.Ingestion;
using AERai.Web.Infrastructure.Persistence;
using AERai.Web.Infrastructure.Promotion;
using AERai.Web.Infrastructure.Queries;
using AERai.Web.Infrastructure.Repositories;
using AERai.Web.Infrastructure.SpApi;
using AERai.Web.Infrastructure.Spreadsheets;
using AERai.Web.Infrastructure.Storage;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure;

/// <summary>
/// Registers Infrastructure implementations of the Application abstractions.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Connection string name in configuration (<c>ConnectionStrings:Sql</c>).</summary>
    public const string ConnectionStringName = "Sql";

    /// <summary>
    /// Adds the SQL Server <see cref="AppDbContext"/>, ASP.NET Core Identity stores, the raw blob
    /// landing zone, and the query/repository/promotion/identity services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration (must contain <c>ConnectionStrings:Sql</c>).</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The SQL connection string or raw storage settings are missing.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it with dotnet user-secrets " +
                $"(ConnectionStrings:{ConnectionStringName}) or the ConnectionStrings__{ConnectionStringName} environment variable.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString, ConfigureSqlServer));

        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 12;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Re-check each session's security stamp every minute (Identity's default is 30), so a password
        // change, reset, or admin lock signs out the account's other sessions within a minute.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        // Reset links (and other Identity data-protection tokens) expire after an hour, matching the email text.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = PasswordResetService.LinkLifetime);

        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        AddRawStorage(services, configuration);

        services.AddScoped<IStagingRepository, StagingRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IImportBatchQueries, ImportBatchQueries>();
        services.AddScoped<IMarketplaceQueries, MarketplaceQueries>();
        services.AddScoped<IReportingQueries, ReportingQueries>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IInventoryQueries, InventoryQueries>();
        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        services.AddScoped<IProductFamilyRepository, ProductFamilyRepository>();
        services.AddScoped<IHomeStockLedgerRepository, HomeStockLedgerRepository>();
        services.AddScoped<IStockAlertRepository, StockAlertRepository>();
        services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();
        services.AddSingleton<IHomeStockTemplateWriter, ClosedXmlHomeStockTemplateWriter>();
        services.AddScoped<IPromotionService, SqlPromotionService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ISyncScheduleRepository, SyncScheduleRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        services.AddScoped<ISyncSettingsRepository, SyncSettingsRepository>();

        AddSpApi(services, configuration);

        services.AddSingleton<IManualRunChannel, ManualRunChannel>();
        services.AddHostedService<SyncSchedulerWorker>();
        services.AddSingleton<StockAlertRefreshSignal>();
        services.AddSingleton<IStockAlertRefreshSignal>(sp => sp.GetRequiredService<StockAlertRefreshSignal>());
        services.AddHostedService<StockAlertWorker>();

        return services;
    }

    /// <summary>
    /// Registers the Amazon gateway for the configured <see cref="SpApiMode"/>. Live mode wires the
    /// SP-API pipeline: LWA token cache, per-operation rate limiter, and the pipeline handler on the
    /// typed Reports and Catalog Items clients. All SP-API traffic goes through that one handler.
    /// </summary>
    private static void AddSpApi(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SpApiOptions>()
            .BindConfiguration(SpApiOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonConnectionInfo, AmazonConnectionInfo>();
        services.AddSingleton<IAmazonConnectionTester, AmazonConnectionTester>();

        var mode = configuration.GetSection(SpApiOptions.SectionName).GetValue(nameof(SpApiOptions.Mode), SpApiMode.Disabled);
        switch (mode)
        {
            case SpApiMode.Live:
                services.AddSingleton<SpApiRateLimiter>();
                services.AddSingleton<LwaTokenProvider>();
                services.AddTransient<SpApiPipelineHandler>();
                services.AddHttpClient(LwaTokenProvider.HttpClientName);
                services.AddHttpClient(ReportsApiClient.DocumentHttpClientName);
                services.AddHttpClient<ReportsApiClient>((provider, client) =>
                        client.BaseAddress = provider.GetRequiredService<IOptions<SpApiOptions>>().Value.Endpoint)
                    .AddHttpMessageHandler<SpApiPipelineHandler>();
                services.AddScoped<IAmazonReportsGateway, SpApiReportsGateway>();
                services.AddHttpClient<CatalogItemsApiClient>((provider, client) =>
                        client.BaseAddress = provider.GetRequiredService<IOptions<SpApiOptions>>().Value.Endpoint)
                    .AddHttpMessageHandler<SpApiPipelineHandler>();
                services.AddHttpClient(SpApiCatalogGateway.ImageHttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
                services.AddScoped<IAmazonCatalogGateway, SpApiCatalogGateway>();
                break;

            case SpApiMode.Simulated:
                // Singleton: the simulator keeps generated documents in memory between request and download.
                services.AddSingleton<IAmazonReportsGateway, SimulatedReportsGateway>();
                services.AddSingleton<IAmazonCatalogGateway, SimulatedCatalogGateway>();
                break;

            default:
                services.AddSingleton<IAmazonReportsGateway, UnavailableReportsGateway>();
                services.AddSingleton<IAmazonCatalogGateway, UnavailableCatalogGateway>();
                break;
        }
    }

    /// <summary>
    /// Registers the raw container client and <see cref="IRawFileStore"/>. Uses the
    /// <c>ConnectionStrings:RawStorage</c> connection string when present (Azurite), otherwise
    /// <c>RawStorage:ServiceUri</c> with <see cref="DefaultAzureCredential"/> (managed identity in Azure,
    /// so no storage key exists anywhere in configuration).
    /// </summary>
    private static void AddRawStorage(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RawStorageOptions>()
            .BindConfiguration(RawStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString(RawStorageOptions.ConnectionStringName);
        var serviceUri = configuration.GetSection(RawStorageOptions.SectionName).GetValue<Uri?>(nameof(RawStorageOptions.ServiceUri));
        if (string.IsNullOrWhiteSpace(connectionString) && serviceUri is null)
        {
            throw new InvalidOperationException(
                $"Raw storage is not configured. Set ConnectionStrings:{RawStorageOptions.ConnectionStringName} " +
                $"(e.g. UseDevelopmentStorage=true for Azurite) or {RawStorageOptions.SectionName}:{nameof(RawStorageOptions.ServiceUri)}.");
        }

        services.AddSingleton(_ => string.IsNullOrWhiteSpace(connectionString)
            ? new BlobServiceClient(serviceUri, new DefaultAzureCredential())
            : new BlobServiceClient(connectionString));

        services.AddSingleton(provider => provider.GetRequiredService<BlobServiceClient>()
            .GetBlobContainerClient(provider.GetRequiredService<IOptions<RawStorageOptions>>().Value.ContainerName));

        services.AddSingleton<IRawFileStore, BlobRawFileStore>();

        services.AddSingleton(provider => new ProductImageContainer(provider.GetRequiredService<BlobServiceClient>()
            .GetBlobContainerClient(provider.GetRequiredService<IOptions<RawStorageOptions>>().Value.ProductImageContainerName)));
        services.AddSingleton<IProductImageStore, BlobProductImageStore>();
    }

    /// <summary>
    /// SQL Server provider settings shared by the runtime and design-time (<c>dotnet ef</c>) contexts,
    /// so both agree on where the migrations history lives.
    /// </summary>
    /// <param name="sql">The provider options builder.</param>
    internal static void ConfigureSqlServer(SqlServerDbContextOptionsBuilder sql)
    {
        sql.MigrationsHistoryTable("__EFMigrationsHistory", Schemas.Core);

        // Transient-fault retries matter on Azure SQL (failovers, throttling).
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
    }
}
