namespace AERai.Web.Application.Abstractions;

/// <summary>
/// The raw landing zone: durable storage for source files exactly as received, before any parsing.
/// Implemented over Azure Blob Storage (Azurite locally).
/// </summary>
/// <remarks>
/// The store is deliberately dumb — it never interprets content. Path layout is an Application
/// concern (see <see cref="Imports.RawFilePaths"/>), so the same layout applies whatever the backend.
/// </remarks>
public interface IRawFileStore
{
    /// <summary>Writes a new raw file. Existing files are never overwritten.</summary>
    /// <param name="path">Relative path inside the raw container, from <see cref="Imports.RawFilePaths.Build"/>.</param>
    /// <param name="content">File content, read to the end. The caller owns and disposes it.</param>
    /// <param name="metadata">ASCII key/value pairs stored alongside the file (source, uploader, original name).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Lower-case hex SHA-256 of the bytes written.</returns>
    /// <exception cref="InvalidOperationException">A file already exists at <paramref name="path"/>.</exception>
    Task<string> SaveAsync(string path, Stream content, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken);

    /// <summary>Opens a stored raw file for reading.</summary>
    /// <param name="path">Relative path inside the raw container.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A readable stream the caller must dispose, or <see langword="null"/> when the file does not exist.</returns>
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken);
}
