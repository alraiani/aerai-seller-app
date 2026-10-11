namespace AERai.Web.Application.Inventory;

/// <summary>Reading and identifying product-picture bytes, shared by every way a picture arrives.</summary>
internal static class ImageFiles
{
    /// <summary>
    /// Reads a stream to the end, but never more than <paramref name="maxBytes"/>: the declared
    /// length of an upload, a zip entry, or a download is not trusted.
    /// </summary>
    /// <param name="content">The stream; read, not disposed.</param>
    /// <param name="maxBytes">Largest size accepted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or <see langword="null"/> when the stream holds more than <paramref name="maxBytes"/>.</returns>
    public static async Task<byte[]?> ReadAtMostAsync(Stream content, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Identifies JPEG, PNG, or WebP from the file's leading bytes (its signature).</summary>
    /// <param name="bytes">The picture.</param>
    /// <returns>Its MIME type and file extension, or <see langword="null"/> for anything else.</returns>
    public static (string ContentType, string Extension)? DetectType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return ("image/jpeg", ".jpg");
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ("image/png", ".png");
        }

        // RIFF container whose form type (bytes 8-11) is WEBP.
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return ("image/webp", ".webp");
        }

        return null;
    }
}
