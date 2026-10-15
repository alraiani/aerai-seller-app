using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Tests.Settlements;

public sealed class SettlementMoneyTests
{
    private static SettlementSummary Cad(decimal? rate) => new()
    {
        MarketplaceId = "M",
        SettlementId = "S",
        Currency = "CAD",
        HomeCurrency = "USD",
        ExchangeRate = rate,
        Sales = 100m,
        Fees = -15m,
        Net = 85m,
        HomeSales = rate is null ? null : 72m,
        HomeFees = rate is null ? null : -10.8m,
        HomeRefunds = rate is null ? null : 0m,
        HomeOther = rate is null ? null : 0m,
        HomeNet = rate is null ? null : 61.2m,
    };

    [Theory]
    [InlineData("USD", "US")]
    [InlineData("cad", "CA")]
    [InlineData("MXN", "MX")]
    [InlineData("GBP", "UK")]
    [InlineData("EUR", "EUR")]
    [InlineData(null, "")]
    public void FromCurrency_TagsThePayoutWithItsMarketplace(string? currency, string expected) =>
        Assert.Equal(expected, SettlementOrigin.FromCurrency(currency));

    [Fact]
    public void Of_HomeWithARate_ShowsTheConvertedAmounts()
    {
        var money = SettlementMoney.Of(Cad(0.72m), SettlementAmounts.Home);

        Assert.Equal((72m, -10.8m, 61.2m, "USD", true), (money.Sales, money.Fees, money.Net, money.Currency, money.IsConverted));
    }

    [Fact]
    public void Of_HomeWithoutARate_ShowsTheAmountsAsPaid()
    {
        var money = SettlementMoney.Of(Cad(null), SettlementAmounts.Home);

        Assert.Equal((100m, 85m, "CAD", false), (money.Sales, money.Net, money.Currency, money.IsConverted));
    }

    [Fact]
    public void Of_Original_ShowsTheAmountsAsPaid()
    {
        var money = SettlementMoney.Of(Cad(0.72m), SettlementAmounts.Original);

        Assert.Equal((85m, "CAD"), (money.Net, money.Currency));
    }

    [Fact]
    public void Of_SameCurrency_IsNotConverted()
    {
        var usd = new SettlementSummary { MarketplaceId = "M", SettlementId = "S", Currency = "USD", HomeCurrency = "USD", ExchangeRate = 1m, Net = 5m, HomeNet = 5m };

        Assert.False(SettlementMoney.Of(usd, SettlementAmounts.Home).IsConverted);
    }

    [Fact]
    public void AmountOf_Line_FollowsTheSameRule()
    {
        var line = new SettlementLineDetail { MarketplaceId = "M", SettlementId = "S", TransactionType = "Order", Currency = "MXN", HomeCurrency = "USD", ExchangeRate = 0.054m, Amount = 100m, HomeAmount = 5.4m };

        Assert.Equal((5.4m, "USD"), SettlementMoney.AmountOf(line, SettlementAmounts.Home));
        Assert.Equal((100m, "MXN"), SettlementMoney.AmountOf(line, SettlementAmounts.Original));
    }
}
