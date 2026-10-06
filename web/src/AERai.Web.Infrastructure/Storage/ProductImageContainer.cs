using Azure.Storage.Blobs;

namespace AERai.Web.Infrastructure.Storage;

/// <summary>
/// The product-images container client, wrapped in its own type so it can be injected alongside the
/// raw container's plain <see cref="BlobContainerClient"/> without ambiguity.
/// </summary>
/// <param name="Client">The container client.</param>
internal sealed record ProductImageContainer(BlobContainerClient Client);
