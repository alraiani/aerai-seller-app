namespace AERai.Seller.Application.Abstractions;

/// <summary>
/// Writes generated export text (e.g. IIF content) to disk. Kept as an Application interface so
/// pure generation logic (IifExportGenerator) never touches the filesystem directly and stays
/// unit-testable without I/O.
/// </summary>
public interface IExportFileStore
{
    /// <summary>Writes <paramref name="content"/> under <paramref name="fileName"/> and returns the full path written to.</summary>
    Task<string> SaveAsync(string fileName, string content, CancellationToken cancellationToken = default);
}
