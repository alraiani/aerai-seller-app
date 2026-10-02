using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary><see cref="IImportBatchQueries"/> backed by a configurable list of batches.</summary>
internal sealed class FakeImportBatchQueries : IImportBatchQueries
{
    public ImportBatchSummary? Latest { get; set; }

    public List<ImportBatchSummary> Batches { get; } = [];

    public Task<ImportBatchSummary?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult(Latest);

    public Task<PagedResult<ImportBatchSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportBatchSummary?> GetAsync(long batchId, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.FirstOrDefault(b => b.Id == batchId));

    public Task<PagedResult<RejectedRow>> GetRejectedRowsAsync(long batchId, PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
