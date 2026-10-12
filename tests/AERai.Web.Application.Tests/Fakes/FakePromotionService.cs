using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary><see cref="IPromotionService"/> that records which batches were promoted.</summary>
internal sealed class FakePromotionService : IPromotionService
{
    public List<long> Promoted { get; } = [];

    /// <summary>Rows each promotion reports as belonging to another marketplace.</summary>
    public int SkippedRowCount { get; set; }

    public Task<Result<PromotionSummary>> PromoteAsync(long batchId, CancellationToken cancellationToken)
    {
        Promoted.Add(batchId);
        return Task.FromResult(Result.Success(new PromotionSummary(batchId, ImportBatchStatus.Promoted, 1, 0, SkippedRowCount)));
    }
}
