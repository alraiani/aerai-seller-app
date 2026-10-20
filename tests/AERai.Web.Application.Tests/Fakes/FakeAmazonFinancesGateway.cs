using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAmazonFinancesGateway"/>: returns the scripted pages in order, chained by token.</summary>
internal sealed class FakeAmazonFinancesGateway : IAmazonFinancesGateway
{
    public List<IReadOnlyList<DeferredTransactionItem>> Pages { get; } = [];

    public List<DateTimeOffset> PostedAfterSeen { get; } = [];

    public Exception? Failure { get; set; }

    public Task<DeferredTransactionPage> ListTransactionsPageAsync(Marketplace marketplace, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        PostedAfterSeen.Add(postedAfter);
        var index = nextToken is null ? 0 : int.Parse(nextToken, System.Globalization.CultureInfo.InvariantCulture);
        var items = index < Pages.Count ? Pages[index] : [];
        var next = index + 1 < Pages.Count ? (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        return Task.FromResult(new DeferredTransactionPage(items, next, $"{{\"page\":{index}}}"));
    }
}
