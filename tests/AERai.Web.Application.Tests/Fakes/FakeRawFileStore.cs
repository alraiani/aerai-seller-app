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

    /// <summary>When each file was "written"; files without an entry count as written at the epoch.</summary>
    public Dictionary<string, DateTimeOffset> CreatedAt { get; } = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<RawFileInfo>> ListAsync(string prefix, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RawFileInfo>>([.. Files
            .Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(f => new RawFileInfo(f.Key, f.Value.Content.Length, CreatedAt.GetValueOrDefault(f.Key), f.Value.Metadata))
            .OrderBy(f => f.CreatedAt).ThenBy(f => f.Path, StringComparer.Ordinal)]);

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Files.TryGetValue(path, out var file) ? new MemoryStream(file.Content) : null);
}
