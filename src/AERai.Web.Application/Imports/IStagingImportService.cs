using AERai.Web.Application.Common;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Moves source files through the first two hops of the pipeline:
/// source → raw blob storage (landing zone) → <c>stg</c> tables.
/// </summary>
public interface IStagingImportService
{
    /// <summary>
    /// Validates an uploaded file, stores it untouched in raw blob storage, then stages it as a new
    /// <see cref="Domain.Staging.ImportBatch"/> in the <see cref="Domain.Staging.ImportBatchStatus.Received"/> state.
    /// </summary>
    /// <param name="command">The uploaded file and its metadata.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new batch id and row count, or a failure describing why the file was refused.</returns>
    /// <remarks>
    /// A file that passes the metadata checks (type, size) is stored even if its content later fails
    /// parsing: the landing zone keeps everything received, for audit and troubleshooting.
    /// </remarks>
    Task<Result<ImportReceipt>> ImportAsync(ImportFileCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Parses a file already in raw blob storage into a new staging batch.
    /// </summary>
    /// <param name="command">Which raw file to stage, and for whom.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new batch id and row count, or a failure when the file is missing or unparseable.</returns>
    Task<Result<ImportReceipt>> StageRawFileAsync(StageRawFileCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Re-stages an earlier batch from its stored raw file into a <em>new</em> batch (the original
    /// batch and its rows are left untouched). Useful after a mapping or validation fix.
    /// </summary>
    /// <param name="batchId">The earlier batch.</param>
    /// <param name="requestedBy">User requesting the re-stage.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new batch id and row count, or a failure when the batch has no stored raw file.</returns>
    Task<Result<ImportReceipt>> RestageAsync(long batchId, string requestedBy, CancellationToken cancellationToken);
}
