using AERai.Seller.SpApiClient.Awd;
using AERai.Seller.SpApiClient.CatalogItems;
using AERai.Seller.SpApiClient.Orders;
using AERai.Seller.SpApiClient.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Seller.SpApiClient;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SP-API client layer. Requires an <see cref="ICredentialStore"/> implementation
    /// to already be registered by the host app (Infrastructure).
    /// </summary>
    public static IServiceCollection AddSpApiClient(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(LwaTokenProvider));
        services.AddHttpClient(nameof(SpApiRequestPipeline));
        services.AddHttpClient(nameof(ReportsApiClient) + ".Download");

        services.AddSingleton<SpApiOperationRateLimiter>(_ => new SpApiOperationRateLimiter(SpApiRateLimits.Defaults));
        services.AddSingleton<LwaTokenProvider>();
        services.AddSingleton<SpApiRequestPipeline>();
        services.AddSingleton<ReportsApiClient>();
        services.AddSingleton<OrdersApiClient>();
        services.AddSingleton<CatalogItemsApiClient>();
        services.AddSingleton<AwdApiClient>();

        return services;
    }
}
