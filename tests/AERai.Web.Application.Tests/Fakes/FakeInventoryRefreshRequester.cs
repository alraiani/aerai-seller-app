using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>Records <see cref="IInventoryRefreshRequester"/> requests.</summary>
internal sealed class FakeInventoryRefreshRequester : IInventoryRefreshRequester
{
    public List<string> Requests { get; } = [];

    public Task<int> RequestAsync(string marketplaceId, string user, CancellationToken cancellationToken)
    {
        Requests.Add(marketplaceId);
        return Task.FromResult(1);
    }
}
