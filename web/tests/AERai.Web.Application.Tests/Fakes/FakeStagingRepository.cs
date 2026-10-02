using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IStagingRepository"/> that records saved batches.</summary>
internal sealed class FakeStagingRepository : IStagingRepository
{
    public List<ImportBatch> Saved { get; } = [];

    public Task<long> AddBatchAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        batch.Id = Saved.Count + 1;
        Saved.Add(batch);
        return Task.FromResult(batch.Id);
    }
}
