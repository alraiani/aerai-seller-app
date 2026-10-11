using System.Security.Cryptography;
using AERai.Web.Application.Abstractions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Infrastructure.Storage;

/// <summary>
/// <see cref="IRawFileStore"/> over Azure Blob Storage. Files are write-once: an existing blob is
/// never overwritten, which keeps the landing zone a trustworthy record of what was received.
/// </summary>
/// <param name="container">The raw container client (registered in <see cref="DependencyInjection"/>).</param>
/// <param name="logger">Logger.</param>
internal sealed partial class BlobRawFileStore(BlobContainerClient container, ILogger<BlobRawFileStore> logger) : IRawFileStore
{
    /// <inheritdoc/>
    public async Task<string> SaveAsync(string path, Stream content, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        // Hash while uploading (read-through) so the file is streamed once and never buffered in memory.
        using var sha256 = SHA256.Create();
        await using var hashingStream = new CryptoStream(content, sha256, CryptoStreamMode.Read, leaveOpen: true);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = ContentTypeFor(path) },
            Metadata = metadata.ToDictionary(),

            // If-None-Match: * makes the upload fail rather than overwrite an existing blob.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
        };

        try
        {
            await container.GetBlobClient(path).UploadAsync(hashingStream, options, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
        {
            throw new InvalidOperationException($"A raw file already exists at '{path}'.", ex);
        }

        var hash = Convert.ToHexStringLower(sha256.Hash!); // Non-null: the CryptoStream was read to the end by the upload.
        LogSaved(path, hash);
        return hash;
    }

    /// <inheritdoc/>
    public async Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return await container.GetBlobClient(path).OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>Content type by extension, so files preview sensibly in Storage Explorer / the portal.</summary>
    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".csv" => "text/csv",
        ".tsv" => "text/tab-separated-values",
        _ => "text/plain",
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved raw file {RawFilePath} (sha256 {Sha256})")]
    private partial void LogSaved(string rawFilePath, string sha256);
}
