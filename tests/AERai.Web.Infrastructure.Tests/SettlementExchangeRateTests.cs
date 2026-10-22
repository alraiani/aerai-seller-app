using System.Globalization;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// Settlements paid in another currency than their marketplace's (the CAD and MXN payouts Amazon lists
/// with the United States): the deposit date is kept, the simulated feed (CAD 0.72, MXN 0.054 USD) gives
/// them a rate after promotion, the views convert them, and an Operator's received amount is exact.
/// </summary>
public sealed class SettlementExchangeRateTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string Us = MarketplaceIds.UnitedStates;

    private const string Header =
        "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\ttransaction-type\torder-id\tsku\tamount-type\tamount-description\tamount\tposted-date\n";

    private static string Settlement(string id, string currency, string deposit, params (string Type, string Description, decimal Amount)[] lines)
    {
        var builder = new StringBuilder(Header);
        builder.Append(CultureInfo.InvariantCulture, $"{id}\t2026-09-01 00:00:00 UTC\t2026-09-15 00:00:00 UTC\t{deposit}\t\t{currency}\t\t\t\t\t\t\t\n");
        foreach (var (type, description, amount) in lines)
        {
            var transaction = type == "ItemPrice" || type == "ItemFees" ? "Order" : "other-transaction";
            builder.Append(CultureInfo.InvariantCulture, $"{id}\t\t\t\t\t\t{transaction}\t{id}-O\tFX-SKU\t{type}\t{description}\t{amount:0.00}\t2026-09-05\n");
        }

        return builder.ToString();
    }

    private async Task PromoteAsync(string tsv)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(tsv);
        var staged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(ImportSource.Settlements, Us, "settlement.tsv", bytes.Length, new MemoryStream(bytes), "tests"), CancellationToken.None);
        Assert.True(staged.IsSuccess, staged.Error);
        var promoted = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(staged.Value.BatchId, CancellationToken.None);
        Assert.True(promoted.IsSuccess, promoted.Error);
    }

    private async Task<Domain.Reporting.SettlementSummary> SummaryAsync(string id)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var summary = await scope.ServiceProvider.GetRequiredService<IReportingQueries>().GetSettlementAsync(Us, id, CancellationToken.None);
        Assert.NotNull(summary);
        return summary;
    }

    [SqlFact]
    public async Task Promotion_CadSettlement_KeepsDepositDateAndConvertsAtTheFetchedRate()
    {
        await PromoteAsync(Settlement("FX-CAD-1", "CAD", "17.09.2026 01:50:49 UTC",
            ("ItemPrice", "Principal", 100.01m), ("ItemFees", "Commission", -15.03m), ("other-transaction", "Current Reserve Amount", 2.33m)));

        var s = await SummaryAsync("FX-CAD-1");

        Assert.Equal(new DateTimeOffset(2026, 9, 17, 1, 50, 49, TimeSpan.Zero), s.DepositDate);
        Assert.Equal(("CAD", "USD", 0.72m, "Market"), (s.Currency, s.HomeCurrency, s.ExchangeRate, s.RateSource));
        Assert.Equal(87.31m, s.Net);

        // The net converts as a whole (87.31 × 0.72 = 62.8632), and the four columns add up to it exactly:
        // expenses take the cent the separate roundings leave over (-15.03 × 0.72 = -10.8216 → -10.83).
        Assert.Equal(62.86m, s.HomeNet);
        Assert.Equal((72.01m, 0m, -10.83m, 1.68m), (s.HomeSales, s.HomeRefunds, s.HomeExpenses, s.HomeReserve));
        Assert.Equal(s.HomeNet, s.HomeSales + s.HomeExpenses + s.HomeRefunds + s.HomeReserve);
    }

    [SqlFact]
    public async Task Views_SettlementInTheMarketplaceCurrency_NeedsNoRate()
    {
        await PromoteAsync(Settlement("FX-USD-1", "USD", "2026-09-17 01:50:49 UTC", ("ItemPrice", "Principal", 50m)));

        var s = await SummaryAsync("FX-USD-1");

        Assert.Equal((1m, "Same", 50m), (s.ExchangeRate, s.RateSource, s.HomeNet));
        await using var scope = fixture.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().SettlementExchangeRates.AnyAsync(x => x.SettlementId == "FX-USD-1"));
    }

    [SqlFact]
    public async Task Views_DepositInTheFuture_WaitsForItsRateAndIsLeftOutOfTotals()
    {
        await PromoteAsync(Settlement("FX-MXN-FUTURE", "MXN", "2099-01-01 00:00:00 UTC", ("ItemPrice", "Principal", 500m)));

        var s = await SummaryAsync("FX-MXN-FUTURE");
        Assert.Null(s.ExchangeRate);
        Assert.Null(s.HomeNet);

        await using var scope = fixture.Services.CreateAsyncScope();
        var totals = await scope.ServiceProvider.GetRequiredService<IReportingQueries>()
            .GetSettlementTotalsAsync(Us, new PageRequest(Search: "FX-MXN-FUTURE"), null, CancellationToken.None);
        Assert.Equal((0, 1, 0m), (totals!.Count, totals.AwaitingRate, totals.Net));
    }

    [SqlFact]
    public async Task SetManual_AmountReceived_IsTheConvertedPayoutExactly_AndResetGoesBack()
    {
        await PromoteAsync(Settlement("FX-CAD-2", "CAD", "2026-09-17 01:50:49 UTC", ("ItemPrice", "Principal", 333.33m), ("ItemFees", "Commission", -33.33m)));
        await using var scope = fixture.Services.CreateAsyncScope();
        var rates = scope.ServiceProvider.GetRequiredService<ISettlementExchangeRateService>();

        var set = await rates.SetManualAsync(Us, "FX-CAD-2", homeAmount: 219.13m, rate: null, "ops@example.com", CancellationToken.None);
        Assert.True(set.IsSuccess, set.Error);

        var adjusted = await SummaryAsync("FX-CAD-2");
        Assert.Equal(("Manual", 219.13m), (adjusted.RateSource, adjusted.HomeNet));
        Assert.Equal(adjusted.HomeNet, adjusted.HomeSales + adjusted.HomeExpenses + adjusted.HomeRefunds + adjusted.HomeReserve);

        // The export posts the deposit the bank shows, and the entry still balances.
        await scope.ServiceProvider.GetRequiredService<Application.Bookkeeping.IBookkeepingService>().SaveMappingsAsync(
            Us, "Checking",
            [new("ItemPrice", "Principal", "Sales"), new("ItemFees", "Commission", "Fees")],
            "ops@example.com", CancellationToken.None);
        var preview = await scope.ServiceProvider.GetRequiredService<Application.Bookkeeping.IBookkeepingService>().PreviewAsync(Us, "FX-CAD-2", CancellationToken.None);
        Assert.True(preview.IsSuccess, preview.Error);
        Assert.Equal(("USD", 219.13m, "CAD", 300.00m), (preview.Value.Currency, preview.Value.NetTotal, preview.Value.OriginalCurrency, preview.Value.OriginalNetTotal));
        Assert.Equal(219.13m, preview.Value.AccountTotals!.Sum(a => a.Amount));

        var reset = await rates.ResetAsync(Us, "FX-CAD-2", CancellationToken.None);
        Assert.True(reset.IsSuccess, reset.Error);
        Assert.Equal(("Market", 216.00m), ((await SummaryAsync("FX-CAD-2")).RateSource, (await SummaryAsync("FX-CAD-2")).HomeNet));
    }

    [SqlFact]
    public async Task SettlementsRun_FromCanada_IsKeptUnderTheUnitedStates()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var schedules = scope.ServiceProvider.GetRequiredService<ISyncScheduleService>();
        var created = await schedules.CreateAsync(
            new SyncScheduleInput("ca-settlements", AmazonReportType.Settlements, MarketplaceIds.Canada, IsEnabled: true, ScheduleFrequency.Interval, 60, null, "UTC", 30, AutoPromote: true),
            "tests", CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error);

        var run = await scope.ServiceProvider.GetRequiredService<IReportIngestionService>().RunAsync(created.Value, SyncTrigger.Manual, "tests", backfill: null, CancellationToken.None);
        Assert.True(run.Status == SyncRunStatus.Succeeded, run.Message);

        // Amazon lists the whole region's payouts; every one of them lands in the United States' list.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var simulated = await db.Settlements.Where(s => EF.Functions.Like(s.SettlementId, "CA-%") || EF.Functions.Like(s.SettlementId, "MX-%") || EF.Functions.Like(s.SettlementId, "US-%")).ToListAsync();
        Assert.NotEmpty(simulated);
        Assert.All(simulated, s => Assert.Equal(Us, s.MarketplaceId));
        Assert.Contains(simulated, s => s.Currency == "CAD");
        Assert.Contains(simulated, s => s.Currency == "MXN");
        Assert.True(await db.ImportBatches.Where(b => b.Source == ImportSource.Settlements && b.UploadedBy == "tests" && EF.Functions.Like(b.FileName, "Settlements-SIM-SETTLE-%")).AllAsync(b => b.MarketplaceId == Us));
    }
}
