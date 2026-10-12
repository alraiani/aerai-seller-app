using AERai.Web.Application.Abstractions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AERai.Web.Infrastructure.Storage;

/// <summary>
/// <see cref="IProductImageStore"/> over its own Azure Blob Storage container, separate from the
/// write-once raw landing zone. Pictures are served through the app (the account has no public or
/// shared-key access), never directly from storage.
/// </summary>
/// <param name="container">The product-images container client.</param>
internal sealed class BlobProductImageStore(ProductImageContainer container) : IProductImageStore
{
    /// <inheritdoc/>
    public async Task SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        await container.Client.GetBlobClient(path)
            .UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return await container.Client.GetBlobClient(path).OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return container.Client.GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
