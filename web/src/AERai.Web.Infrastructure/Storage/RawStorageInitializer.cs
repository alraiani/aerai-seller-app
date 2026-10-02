using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Storage;

/// <summary>
/// Startup routine that makes sure the raw container exists (idempotent).
/// </summary>
public static class RawStorageInitializer
{
    /// <summary>Creates the raw container if it is missing. Containers are always private.</summary>
    /// <param name="services">The application's root service provider.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the container exists.</returns>
    public static async Task InitializeRawStorageAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await services.GetRequiredService<BlobContainerClient>().CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
