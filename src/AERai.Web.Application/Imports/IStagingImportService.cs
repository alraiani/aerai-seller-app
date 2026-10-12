using AERai.Web.Application.Common;
using AERai.Web.Domain.Staging;

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
    /// Works out which report a file is from its header row, so the user need not say. Exactly one report type's
    /// required columns must all be present; the file is only read as far as its first line.
    /// </summary>
    /// <param name="content">The file content. The caller owns and disposes it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report type, or a failure when the header matches none or more than one (the message says which).</returns>
    Task<Result<ImportSource>> DetectSourceAsync(Stream content, CancellationToken cancellationToken);

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
