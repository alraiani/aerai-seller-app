using System.Security.Cryptography;
using AERai.Web.Application.Abstractions;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// Stand-in raw store for SQL tests when no blob emulator is configured, so the database tests
/// don't depend on Azurite. Blob behavior itself is covered by <see cref="BlobRawFileStoreTests"/>.
/// </summary>
internal sealed class InMemoryRawFileStore : IRawFileStore
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public async Task<string> SaveAsync(string path, Stream content, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _files.Add(path, buffer.ToArray());
        return Convert.ToHexStringLower(SHA256.HashData(_files[path]));
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(_files.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : null);
}
