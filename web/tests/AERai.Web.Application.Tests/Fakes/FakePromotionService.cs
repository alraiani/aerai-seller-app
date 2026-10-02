using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary><see cref="IPromotionService"/> that records which batches were promoted.</summary>
internal sealed class FakePromotionService : IPromotionService
{
    public List<long> Promoted { get; } = [];

    public Task<Result<PromotionSummary>> PromoteAsync(long batchId, CancellationToken cancellationToken)
    {
        Promoted.Add(batchId);
        return Task.FromResult(Result.Success(new PromotionSummary(batchId, ImportBatchStatus.Promoted, 1, 0)));
    }
}
