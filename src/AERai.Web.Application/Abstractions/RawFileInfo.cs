namespace AERai.Web.Application.Abstractions;

/// <summary>A file in the raw landing zone, as listed (its content is not read).</summary>
/// <param name="Path">Relative path inside the raw container.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="CreatedAt">When the file was written to storage.</param>
/// <param name="Metadata">The key/value pairs stored alongside the file; empty for a file uploaded by hand.</param>
public sealed record RawFileInfo(string Path, long Size, DateTimeOffset CreatedAt, IReadOnlyDictionary<string, string> Metadata);
