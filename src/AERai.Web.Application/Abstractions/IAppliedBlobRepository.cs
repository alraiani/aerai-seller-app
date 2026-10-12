using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>Remembers which raw files have already been loaded into the database.</summary>
public interface IAppliedBlobRepository
{
    /// <summary>
    /// Gets the path of every raw file already loaded: files in the ledger, plus every file an import
    /// batch was staged from (uploads, scheduled report pulls, and earlier refreshes).
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The paths, compared ordinally.</returns>
    Task<IReadOnlySet<string>> GetLoadedPathsAsync(CancellationToken cancellationToken);

    /// <summary>Records a file as loaded. Recording a path twice is not an error.</summary>
    /// <param name="blob">The file and what loading it did.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when recorded.</returns>
    Task MarkAppliedAsync(AppliedBlob blob, CancellationToken cancellationToken);
}
