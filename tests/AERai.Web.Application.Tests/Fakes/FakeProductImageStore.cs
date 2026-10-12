using AERai.Web.Application.Abstractions;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IProductImageStore"/>.</summary>
internal sealed class FakeProductImageStore : IProductImageStore
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = new(StringComparer.Ordinal);

    public async Task SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Blobs[path] = (buffer.ToArray(), contentType);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Blobs.TryGetValue(path, out var blob) ? new MemoryStream(blob.Bytes) : null);

    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        Blobs.Remove(path);
        return Task.CompletedTask;
    }
}
