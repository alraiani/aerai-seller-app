using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAmazonAwdGateway"/>: returns the scripted pages in order, chained by token.</summary>
internal sealed class FakeAmazonAwdGateway : IAmazonAwdGateway
{
    public List<IReadOnlyList<AwdInventoryItem>> Pages { get; } = [];

    public List<string?> TokensSeen { get; } = [];

    public Exception? Failure { get; set; }

    /// <summary>When set, every page claims another follows (a listing that never ends).</summary>
    public bool NeverEnds { get; set; }

    public Task<AwdInventoryPage> ListInventoryPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        TokensSeen.Add(nextToken);
        var index = nextToken is null ? 0 : int.Parse(nextToken, System.Globalization.CultureInfo.InvariantCulture);
        var items = index < Pages.Count ? Pages[index] : [];
        var next = NeverEnds || index + 1 < Pages.Count ? (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        return Task.FromResult(new AwdInventoryPage(items, next, $"{{\"page\":{index}}}"));
    }
}
