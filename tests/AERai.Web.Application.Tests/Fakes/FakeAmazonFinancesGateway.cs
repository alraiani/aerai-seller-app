using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAmazonFinancesGateway"/>: returns each listing's scripted pages in order, chained by token.</summary>
internal sealed class FakeAmazonFinancesGateway : IAmazonFinancesGateway
{
    /// <summary>Pages of the still-deferred listing.</summary>
    public List<IReadOnlyList<AmazonDeferredTransaction>> Pages { get; } = [];

    /// <summary>Pages of the released listing.</summary>
    public List<IReadOnlyList<AmazonDeferredTransaction>> ReleasedPages { get; } = [];

    public List<(DeferredTransactionListing Listing, DateTimeOffset PostedAfter)> Calls { get; } = [];

    public Exception? Failure { get; set; }

    /// <summary>When set, only the released listing fails.</summary>
    public Exception? ReleasedFailure { get; set; }

    public Task<DeferredTransactionPage> ListTransactionsPageAsync(
        Marketplace marketplace, DeferredTransactionListing listing, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        if (listing == DeferredTransactionListing.Released && ReleasedFailure is not null)
        {
            throw ReleasedFailure;
        }

        Calls.Add((listing, postedAfter));
        var pages = listing == DeferredTransactionListing.Released ? ReleasedPages : Pages;
        var index = nextToken is null ? 0 : int.Parse(nextToken, System.Globalization.CultureInfo.InvariantCulture);
        var items = index < pages.Count ? pages[index] : [];
        var next = index + 1 < pages.Count ? (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        return Task.FromResult(new DeferredTransactionPage(items, next, $"{{\"page\":{index}}}"));
    }
}
