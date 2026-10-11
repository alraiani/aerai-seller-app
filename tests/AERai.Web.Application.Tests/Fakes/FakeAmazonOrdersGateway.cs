using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAmazonOrdersGateway"/>: returns the scripted pages in order, chained by token.</summary>
internal sealed class FakeAmazonOrdersGateway : IAmazonOrdersGateway
{
    public List<IReadOnlyList<AmazonOrder>> Pages { get; } = [];

    public List<string?> TokensSeen { get; } = [];

    public List<AmazonOrdersFilter> FiltersSeen { get; } = [];

    public Exception? Failure { get; set; }

    /// <summary>When set, every page claims another follows (a search that never ends).</summary>
    public bool NeverEnds { get; set; }

    public Task<AmazonOrdersPage> SearchOrdersPageAsync(Marketplace marketplace, AmazonOrdersFilter filter, string? nextToken, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        TokensSeen.Add(nextToken);
        FiltersSeen.Add(filter);
        var index = nextToken is null ? 0 : int.Parse(nextToken, CultureInfo.InvariantCulture);
        var orders = index < Pages.Count ? Pages[index] : [];
        var next = NeverEnds || index + 1 < Pages.Count ? (index + 1).ToString(CultureInfo.InvariantCulture) : null;
        return Task.FromResult(new AmazonOrdersPage(orders, next, $"{{\"page\":{index}}}"));
    }
}
