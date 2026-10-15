using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Settlements;

public sealed class SettlementExchangeRateServiceTests
{
    private const string Us = MarketplaceIds.UnitedStates;
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeRepository _repository = new();
    private readonly FakeProvider _provider = new();
    private readonly SettlementExchangeRateService _service;

    public SettlementExchangeRateServiceTests() =>
        _service = new SettlementExchangeRateService(_repository, _provider, new FakeTimeProvider(Now), NullLogger<SettlementExchangeRateService>.Instance);

    private static SettlementRateContext Cad(decimal net, SettlementExchangeRate? rate = null) =>
        new("S1", "CAD", "USD", net, new DateOnly(2026, 9, 17), rate, IsExported: false);

    [Fact]
    public async Task FillMissingAsync_AsksOncePerCurrencyAndDay_AndSavesEachSettlement()
    {
        var day = new DateOnly(2026, 9, 17);
        _repository.Needs = [new("A", "CAD", "USD", day), new("B", "CAD", "USD", day), new("C", "MXN", "USD", day)];
        _provider.Rates[("CAD", "USD")] = 0.73m;

        var result = await _service.FillMissingAsync(CancellationToken.None);

        Assert.Equal((2, 1), (result.Filled, result.Unavailable));
        Assert.Equal(["A", "B"], _repository.MarketRates.Keys.Order());
        Assert.Equal(2, _provider.Calls);
        Assert.Equal(DateOnly.FromDateTime(Now.UtcDateTime), _repository.AsOf);
    }

    [Fact]
    public async Task FillMissingAsync_FeedUnreachable_CountsAsUnavailable()
    {
        _repository.Needs = [new("A", "CAD", "USD", new DateOnly(2026, 9, 17))];
        _provider.Throw = true;

        var result = await _service.FillMissingAsync(CancellationToken.None);

        Assert.Equal((0, 1), (result.Filled, result.Unavailable));
        Assert.Empty(_repository.MarketRates);
    }

    [Fact]
    public async Task SetManualAsync_AmountReceived_DerivesTheRateAndKeepsTheAmount()
    {
        _repository.Context = Cad(net: 300m);

        var result = await _service.SetManualAsync(Us, "S1", homeAmount: 219.13m, rate: null, "ops@example.com", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var (rate, amount) = _repository.Manual!.Value;
        Assert.Equal(0.7304333333m, rate);
        Assert.Equal(219.13m, amount);
    }

    [Fact]
    public async Task SetManualAsync_Rate_IsStoredWithoutAnAmount()
    {
        _repository.Context = Cad(net: 300m);

        var result = await _service.SetManualAsync(Us, "S1", homeAmount: null, rate: 0.7312m, "ops@example.com", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((0.7312m, (decimal?)null), _repository.Manual);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(10.0, 0.7)]
    public async Task SetManualAsync_NeitherOrBoth_IsRefused(double? amount, double? rate)
    {
        _repository.Context = Cad(net: 300m);

        var result = await _service.SetManualAsync(Us, "S1", (decimal?)amount, (decimal?)rate, "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(_repository.Manual);
    }

    [Fact]
    public async Task SetManualAsync_AmountForAZeroPayout_IsRefused()
    {
        _repository.Context = Cad(net: 0m);

        var result = await _service.SetManualAsync(Us, "S1", homeAmount: 5m, rate: null, "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("enter the rate", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetManualAsync_AmountWithTheWrongSign_IsRefused()
    {
        _repository.Context = Cad(net: 300m);

        var result = await _service.SetManualAsync(Us, "S1", homeAmount: -219m, rate: null, "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task SetManualAsync_RateOutOfRange_IsRefused(double rate)
    {
        _repository.Context = Cad(net: 300m);

        var result = await _service.SetManualAsync(Us, "S1", homeAmount: null, rate: (decimal)rate, "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SetManualAsync_SettlementInTheMarketplaceCurrency_IsRefused()
    {
        _repository.Context = new SettlementRateContext("S1", "USD", "USD", 100m, null, null, false);

        var result = await _service.SetManualAsync(Us, "S1", homeAmount: 100m, rate: null, "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("nothing to convert", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetAsync_WithoutAnOverride_IsRefused_AndWithOneClearsIt()
    {
        _repository.Context = Cad(net: 300m, new SettlementExchangeRate { SettlementId = "S1", FromCurrency = "CAD", ToCurrency = "USD", MarketRate = 0.72m });
        Assert.True((await _service.ResetAsync(Us, "S1", CancellationToken.None)).IsFailure);

        _repository.Context = Cad(net: 300m, new SettlementExchangeRate { SettlementId = "S1", FromCurrency = "CAD", ToCurrency = "USD", MarketRate = 0.72m, ManualRate = 0.73m });
        var result = await _service.ResetAsync(Us, "S1", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(_repository.Cleared);
    }

    private sealed class FakeProvider : IExchangeRateProvider
    {
        public Dictionary<(string, string), decimal> Rates { get; } = [];

        public int Calls { get; private set; }

        public bool Throw { get; set; }

        public Task<ExchangeRateQuote?> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken)
        {
            Calls++;
            if (Throw)
            {
                throw new HttpRequestException("down");
            }

            return Task.FromResult(Rates.TryGetValue((fromCurrency, toCurrency), out var rate) ? new ExchangeRateQuote(rate, date, "Test") : null);
        }
    }

    private sealed class FakeRepository : ISettlementExchangeRateRepository
    {
        public IReadOnlyList<SettlementRateNeed> Needs { get; set; } = [];

        public DateOnly? AsOf { get; private set; }

        public SettlementRateContext? Context { get; set; }

        public Dictionary<string, ExchangeRateQuote> MarketRates { get; } = [];

        public (decimal Rate, decimal? Amount)? Manual { get; private set; }

        public bool Cleared { get; private set; }

        public Task<IReadOnlyList<SettlementRateNeed>> ListNeedingMarketRateAsync(DateOnly asOf, int max, CancellationToken cancellationToken)
        {
            AsOf = asOf;
            return Task.FromResult(Needs);
        }

        public Task<SettlementRateContext?> GetContextAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken) => Task.FromResult(Context);

        public Task SaveMarketRateAsync(SettlementRateNeed need, ExchangeRateQuote quote, DateTimeOffset fetchedAt, CancellationToken cancellationToken)
        {
            MarketRates[need.SettlementId] = quote;
            return Task.CompletedTask;
        }

        public Task SaveManualAsync(string settlementId, string fromCurrency, string toCurrency, decimal rate, decimal? homeAmount, string by, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Manual = (rate, homeAmount);
            return Task.CompletedTask;
        }

        public Task ClearManualAsync(string settlementId, CancellationToken cancellationToken)
        {
            Cleared = true;
            return Task.CompletedTask;
        }
    }
}
