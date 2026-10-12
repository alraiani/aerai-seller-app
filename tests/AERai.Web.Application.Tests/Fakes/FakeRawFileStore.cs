using System.Security.Cryptography;
using AERai.Web.Application.Abstractions;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory, write-once <see cref="IRawFileStore"/>.</summary>
internal sealed class FakeRawFileStore : IRawFileStore
{
    public Dictionary<string, (byte[] Content, IReadOnlyDictionary<string, string> Metadata)> Files { get; } = new(StringComparer.Ordinal);

    public async Task<string> SaveAsync(string path, Stream content, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        if (Files.ContainsKey(path))
        {
            throw new InvalidOperationException($"A raw file already exists at '{path}'.");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        Files[path] = (bytes, metadata);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Files.TryGetValue(path, out var file) ? new MemoryStream(file.Content) : null);
}
