using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAppliedBlobRepository"/>. Like the real one, a file counts as loaded when it is in
/// the ledger or when an import batch was staged from it.
/// </summary>
internal sealed class FakeAppliedBlobRepository(FakeStagingRepository staged) : IAppliedBlobRepository
{
    public List<AppliedBlob> Ledger { get; } = [];

    public Task<IReadOnlySet<string>> GetLoadedPathsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(
            Ledger.Select(b => b.Path).Concat(staged.Saved.Where(b => b.RawFilePath is not null).Select(b => b.RawFilePath!)),
            StringComparer.Ordinal));

    public Task MarkAppliedAsync(AppliedBlob blob, CancellationToken cancellationToken)
    {
        if (Ledger.All(b => b.Path != blob.Path))
        {
            Ledger.Add(blob);
        }

        return Task.CompletedTask;
    }
}
