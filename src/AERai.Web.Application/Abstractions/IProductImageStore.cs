namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Storage for product pictures. Unlike <see cref="IRawFileStore"/> it is not write-once: a picture
/// can be replaced or removed, so callers delete the old blob after a replacement succeeds.
/// </summary>
public interface IProductImageStore
{
    /// <summary>Stores a picture at a new path.</summary>
    /// <param name="path">Container-relative path (unique per upload).</param>
    /// <param name="content">Picture bytes; read to the end, not disposed.</param>
    /// <param name="contentType">MIME type served with the picture.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the picture is stored.</returns>
    Task SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Opens a stored picture.</summary>
    /// <param name="path">Container-relative path.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A readable stream the caller disposes, or <see langword="null"/> when nothing is stored there.</returns>
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>Deletes a picture; does nothing when it does not exist.</summary>
    /// <param name="path">Container-relative path.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the picture is gone.</returns>
    Task DeleteAsync(string path, CancellationToken cancellationToken);
}
