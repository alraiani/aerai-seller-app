using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IImportBatchQueries"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class ImportBatchQueries(AppDbContext dbContext) : IImportBatchQueries
{
    /// <inheritdoc/>
    public Task<PagedResult<ImportBatchSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.ImportBatches.AsNoTracking();
        if (request.SafeSearch is { } search)
        {
            query = query.Where(b => b.FileName.Contains(search));
        }

        return query
            .OrderByDescending(b => b.Id)
            .Select(ToSummary)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ImportBatchSummary?> GetAsync(long batchId, CancellationToken cancellationToken) =>
        dbContext.ImportBatches.AsNoTracking().Where(b => b.Id == batchId).Select(ToSummary).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<ImportBatchSummary?> GetLatestAsync(CancellationToken cancellationToken) =>
        dbContext.ImportBatches.AsNoTracking().OrderByDescending(b => b.Id).Select(ToSummary).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<PagedResult<RejectedRow>> GetRejectedRowsAsync(long batchId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = await dbContext.ImportBatches
            .Where(b => b.Id == batchId)
            .Select(b => (ImportSource?)b.Source)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Each source keeps its raw rows in its own staging table; query the one this batch used.
        IQueryable<StagingRow>? rows = source switch
        {
            ImportSource.Orders => dbContext.StgOrderLines,
            ImportSource.Inventory => dbContext.StgInventoryRows,
            ImportSource.Settlements => dbContext.StgSettlementLines,
            ImportSource.FbaInventory => dbContext.StgFbaInventoryRows,
            _ => null,
        };

        if (rows is null)
        {
            return new PagedResult<RejectedRow>([], 0, request.SafePage, request.SafePageSize);
        }

        return await rows
            .AsNoTracking()
            .Where(r => r.ImportBatchId == batchId && r.ErrorMessage != null)
            .OrderBy(r => r.RowNumber)
            // ErrorMessage is non-null by the Where filter above.
            .Select(r => new RejectedRow(r.RowNumber, r.ErrorMessage!, r.RawLine))
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Projection shared by every batch query so the DTO shape is defined once.</summary>
    private static readonly System.Linq.Expressions.Expression<Func<ImportBatch, ImportBatchSummary>> ToSummary = b =>
        new ImportBatchSummary(b.Id, b.Source, b.FileName, b.UploadedBy, b.UploadedAt, b.Status,
            b.RowCount, b.PromotedRowCount, b.RejectedRowCount, b.PromotedAt, b.ErrorMessage, b.RawFilePath, b.RawFileSha256);
}
