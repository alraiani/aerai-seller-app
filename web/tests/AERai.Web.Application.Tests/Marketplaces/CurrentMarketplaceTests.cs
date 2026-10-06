using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Marketplaces;

public sealed class CurrentMarketplaceTests
{
    private readonly FakeMarketplaceQueries _marketplaces = new();
    private readonly FakeMarketplacePreference _preference = new();

    private CurrentMarketplace CreateService() => new(_marketplaces, _preference);

    [Fact]
    public async Task GetAsync_NoPreference_UsesFirstActiveMarketplace()
    {
        var selection = await CreateService().GetAsync(CancellationToken.None);

        Assert.Equal(MarketplaceIds.UnitedStates, selection.Current.MarketplaceId);
        Assert.Equal(3, selection.All.Count); // Inactive marketplaces are still listed (shown as not set up).
    }

    [Fact]
    public async Task GetAsync_ActivePreference_UsesIt()
    {
        _preference.Stored = MarketplaceIds.Canada;

        var selection = await CreateService().GetAsync(CancellationToken.None);

        Assert.Equal(MarketplaceIds.Canada, selection.Current.MarketplaceId);
    }

    [Theory]
    [InlineData(MarketplaceIds.UnitedKingdom)] // inactive
    [InlineData("tampered")]
    public async Task GetAsync_UnusablePreference_FallsBackToFirstActive(string stored)
    {
        _preference.Stored = stored;

        var selection = await CreateService().GetAsync(CancellationToken.None);

        Assert.Equal(MarketplaceIds.UnitedStates, selection.Current.MarketplaceId);
    }

    [Fact]
    public async Task SelectAsync_ActiveMarketplace_RemembersAndSwitches()
    {
        var service = CreateService();

        var result = await service.SelectAsync(MarketplaceIds.Canada, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MarketplaceIds.Canada, _preference.Stored);
        Assert.Equal(MarketplaceIds.Canada, (await service.GetAsync(CancellationToken.None)).Current.MarketplaceId);
    }

    [Theory]
    [InlineData(MarketplaceIds.UnitedKingdom)]
    [InlineData("unknown")]
    public async Task SelectAsync_InactiveOrUnknown_FailsWithoutRemembering(string marketplaceId)
    {
        var result = await CreateService().SelectAsync(marketplaceId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(_preference.Stored);
    }

    [Fact]
    public void Resolve_NoActiveMarketplace_Throws()
    {
        var all = TestMarketplaces.All.Select(m => { m.IsActive = false; return m; }).ToList();

        Assert.Throws<InvalidOperationException>(() => MarketplaceSelection.Resolve(all, null));
    }
}
