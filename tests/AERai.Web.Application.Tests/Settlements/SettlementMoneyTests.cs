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
        Expenses = -15m,
        Net = 85m,
        HomeSales = rate is null ? null : 72m,
        HomeExpenses = rate is null ? null : -10.8m,
        HomeRefunds = rate is null ? null : 0m,
        HomeReserve = rate is null ? null : 0m,
        HomeNet = rate is null ? null : 61.2m,
        Commission = -10m,
        OtherExpenses = -5m,
        HomeCommission = rate is null ? null : -7.2m,
        HomeOtherExpenses = rate is null ? null : -3.6m,
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

        Assert.Equal((72m, -10.8m, 61.2m, "USD", true), (money.Sales, money.Expenses, money.Net, money.Currency, money.IsConverted));
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

    [Fact]
    public void BreakdownOf_HomeWithARate_ShowsTheConvertedCategoriesThatAddUpToExpenses()
    {
        var parts = SettlementBreakdown.Of(Cad(0.72m), SettlementAmounts.Home);

        Assert.Equal((-7.2m, -3.6m, "USD", true), (parts.Commission, parts.OtherExpenses, parts.Currency, parts.IsConverted));
        Assert.Equal(-10.8m, parts.Expenses);
    }

    [Fact]
    public void BreakdownOf_Original_ShowsTheCategoriesAsPaid()
    {
        var parts = SettlementBreakdown.Of(Cad(0.72m), SettlementAmounts.Original);

        Assert.Equal((-10m, -5m, -15m, "CAD", false), (parts.Commission, parts.OtherExpenses, parts.Expenses, parts.Currency, parts.IsConverted));
    }
}
