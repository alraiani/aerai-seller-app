using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Infrastructure.Promotion;

/// <summary>
/// <see cref="IPromotionService"/> that delegates to the set-based <c>core.usp_PromoteImportBatch</c> procedure.
/// </summary>
/// <remarks>
/// Promotion is done in T-SQL rather than C# so that conversion, de-duplication, and MERGE run
/// set-based inside one transaction on the server, without pulling staging rows into memory.
/// </remarks>
/// <param name="dbContext">Scoped database context.</param>
/// <param name="batches">Used to confirm the batch exists and to read the outcome.</param>
/// <param name="alerts">Asks for a stock-alert refresh once new data is in core.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class SqlPromotionService(AppDbContext dbContext, IImportBatchQueries batches, IStockAlertRefreshSignal alerts, ILogger<SqlPromotionService> logger)
    : IPromotionService
{
    /// <summary>Large batches can take a while to MERGE; the default 30s command timeout is too tight.</summary>
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public async Task<Result<PromotionSummary>> PromoteAsync(long batchId, CancellationToken cancellationToken)
    {
        if (await batches.GetAsync(batchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<PromotionSummary>($"Import batch {batchId} was not found.");
        }

        dbContext.Database.SetCommandTimeout(CommandTimeout);
        try
        {
            await dbContext.Database
                .ExecuteSqlInterpolatedAsync($"EXEC core.usp_PromoteImportBatch @BatchId = {batchId}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            // The procedure has already rolled back and marked the batch Failed with the reason, so
            // this is reported to the user as an outcome rather than surfaced as an unhandled error.
            LogPromotionFailed(ex, batchId);
            return Result.Failure<PromotionSummary>($"Promotion failed and was rolled back: {ex.Message}");
        }

        var batch = await batches.GetAsync(batchId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Import batch {batchId} disappeared during promotion.");

        LogPromoted(batchId, batch.PromotedRowCount, batch.RejectedRowCount);
        alerts.Request();
        return Result.Success(new PromotionSummary(batch.Id, batch.Status, batch.PromotedRowCount, batch.RejectedRowCount));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Promoted batch {BatchId}: {PromotedRowCount} promoted, {RejectedRowCount} rejected")]
    private partial void LogPromoted(long batchId, int promotedRowCount, int rejectedRowCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Promotion of batch {BatchId} failed")]
    private partial void LogPromotionFailed(Exception exception, long batchId);
}
