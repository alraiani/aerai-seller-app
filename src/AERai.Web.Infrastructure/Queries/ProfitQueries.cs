using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Profit;
using AERai.Web.Domain.Reporting;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IProfitQueries"/> over <c>rpt.vw_ProfitLine</c>, cost history, orders, ad
/// spend, and the settlement views. Added up in SQL wherever possible: a year of ledger lines is far more than a page needs.
/// </summary>
/// <remarks>
/// Ledger lines are dated by the marketplace's local day (<c>AT TIME ZONE</c>, with the Windows name SQL Server
/// knows); settlement lines are date-only, stored at midnight UTC, so their date is used as it is, as on the
/// settlement pages.
/// </remarks>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class ProfitQueries(AppDbContext dbContext) : IProfitQueries
{
    private const string LedgerSource = "Ledger";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfitAmountRow>> GetAmountsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken)
    {
        var zone = WindowsZone(timeZoneId);
        var rows = await InRange(marketplaceId, from, to, timeZoneId)
            .Select(p => new
            {
                Day = p.Source == LedgerSource ? EF.Functions.AtTimeZone(p.PostedDate, zone).Date : p.PostedDate.Date,
                p.Sku,
                p.Category,
                p.Held,
                p.Amount,
            })
            .GroupBy(p => new { p.Day, p.Sku, p.Category, p.Held })
            .Select(g => new { g.Key.Day, g.Key.Sku, g.Key.Category, g.Key.Held, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(r => new ProfitAmountRow(DateOnly.FromDateTime(r.Day), Blank(r.Sku), r.Category, r.Held, r.Amount))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfitSoldUnitsRow>> GetSoldUnitsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken)
    {
        var zone = WindowsZone(timeZoneId);
        var lines = InRange(marketplaceId, from, to, timeZoneId);
        var rows = await (
                from p in lines
                where p.Units > 0 && p.Sku != null && p.Category == ProfitCategory.Sales
                join o in dbContext.Orders on p.AmazonOrderId equals o.AmazonOrderId into orders
                from o in orders.DefaultIfEmpty()
                let costDate = o != null ? o.PurchaseDate : p.PostedDate
                select new
                {
                    Day = p.Source == LedgerSource ? EF.Functions.AtTimeZone(p.PostedDate, zone).Date : p.PostedDate.Date,
                    Sku = p.Sku ?? string.Empty,
                    // The cost in effect on the order's purchase date: the latest change on or before it (a cleared cost is none).
                    UnitCost = dbContext.ProductCostHistory
                        .Where(h => h.MarketplaceId == marketplaceId && h.Sku == p.Sku && h.EffectiveFrom <= costDate)
                        .OrderByDescending(h => h.EffectiveFrom)
                        .ThenByDescending(h => h.Id)
                        .Select(h => h.CostOfGoods)
                        .FirstOrDefault(),
                    p.Units,
                })
            .GroupBy(p => new { p.Day, p.Sku, p.UnitCost })
            .Select(g => new { g.Key.Day, g.Key.Sku, g.Key.UnitCost, Units = g.Sum(p => p.Units) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(r => new ProfitSoldUnitsRow(DateOnly.FromDateTime(r.Day), r.Sku, r.UnitCost, r.Units))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfitRefundRow>> GetRefundsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken)
    {
        var zone = WindowsZone(timeZoneId);
        var lines = InRange(marketplaceId, from, to, timeZoneId);
        var rows = await (
                from p in lines
                where p.IsPrincipal && p.Sku != null && p.Category == ProfitCategory.RefundedSales
                join o in dbContext.Orders on p.AmazonOrderId equals o.AmazonOrderId into orders
                from o in orders.DefaultIfEmpty()
                let costDate = o != null ? o.PurchaseDate : p.PostedDate
                let item = o == null ? null : o.Items.Where(i => i.Sku == p.Sku).OrderBy(i => i.Id).FirstOrDefault()
                select new
                {
                    Day = p.Source == LedgerSource ? EF.Functions.AtTimeZone(p.PostedDate, zone).Date : p.PostedDate.Date,
                    Sku = p.Sku ?? string.Empty,
                    p.Amount,
                    ItemPrice = item == null ? (decimal?)null : item.ItemPrice,
                    ItemQuantity = item == null ? (int?)null : item.Quantity,
                    UnitCost = dbContext.ProductCostHistory
                        .Where(h => h.MarketplaceId == marketplaceId && h.Sku == p.Sku && h.EffectiveFrom <= costDate)
                        .OrderByDescending(h => h.EffectiveFrom)
                        .ThenByDescending(h => h.Id)
                        .Select(h => h.CostOfGoods)
                        .FirstOrDefault(),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(r => new ProfitRefundRow(DateOnly.FromDateTime(r.Day), r.Sku, r.Amount, r.ItemPrice, r.ItemQuantity, r.UnitCost))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfitAdSpendRow>> GetAdSpendAsync(string marketplaceId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await dbContext.AdSpends.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplaceId && s.Date >= from && s.Date <= to)
            .GroupBy(s => new { s.Date, s.Sku })
            .Select(g => new { g.Key.Date, g.Key.Sku, Spend = g.Sum(s => s.Cost) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Where(r => r.Spend != 0).Select(r => new ProfitAdSpendRow(r.Date, r.Sku, r.Spend))];
    }

    /// <inheritdoc/>
    public async Task<DateOnly?> GetFirstAdSpendDayAsync(string marketplaceId, CancellationToken cancellationToken) =>
        await dbContext.AdSpends.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplaceId)
            .MinAsync(s => (DateOnly?)s.Date, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetLedgerStartAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        var first = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(t => t.MarketplaceId == marketplaceId)
            .MinAsync(t => (DateTimeOffset?)t.PostedDate, cancellationToken)
            .ConfigureAwait(false);

        // The same rule as rpt.vw_ProfitLine: the UTC day after the first posted transaction (its first day may be partial).
        return first is { } posted ? new DateTimeOffset(posted.UtcDateTime.Date.AddDays(1), TimeSpan.Zero) : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfitProductRow>> GetProductsAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);
        return await (
                from p in dbContext.Products.AsNoTracking()
                where skus.Contains(p.Sku)
                join f in dbContext.ProductFamilies on p.FamilyId equals f.Id into families
                from f in families.DefaultIfEmpty()
                select new ProfitProductRow(p.Sku, p.Asin, p.Title, p.FamilyId, f == null ? null : f.Name, p.ImagePath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettlementTieRow>> GetSettlementTiesAsync(
        string marketplaceId, DateTimeOffset periodEndFrom, DateTimeOffset periodEndBefore, CancellationToken cancellationToken)
    {
        // A marketplace's payouts are in its settlements list (Canada's in the US one), in its own currency.
        var marketplace = await dbContext.Marketplaces.AsNoTracking()
            .Where(m => m.MarketplaceId == marketplaceId)
            .Select(m => new { List = m.SettlementMarketplaceId ?? m.MarketplaceId, m.Currency })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var settlements = await dbContext.SettlementSummaries.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplace.List && s.Currency == marketplace.Currency
                && s.PeriodEnd >= periodEndFrom && s.PeriodEnd < periodEndBefore)
            .OrderByDescending(s => s.PeriodEnd)
            .Select(s => new { s.SettlementId, s.PeriodStart, s.PeriodEnd, s.Net, s.Reserve })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (settlements.Count == 0)
        {
            return [];
        }

        var ids = settlements.Select(s => s.SettlementId).ToList();
        var ledger = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(t => t.MarketplaceId == marketplaceId && t.SettlementId != null && ids.Contains(t.SettlementId))
            .GroupBy(t => t.SettlementId!)
            .Select(g => new { SettlementId = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToDictionaryAsync(g => g.SettlementId, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        return [.. settlements.Select(s =>
        {
            var found = ledger.GetValueOrDefault(s.SettlementId);
            return new SettlementTieRow(s.SettlementId, s.PeriodStart, s.PeriodEnd, s.Net, s.Reserve, found?.Total, found?.Count ?? 0);
        })];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProfitUnmappedRow>> GetOtherExpenseKindsAsync(
        string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, int take, CancellationToken cancellationToken)
    {
        var rows = await InRange(marketplaceId, from, to, timeZoneId)
            .Where(p => p.Source == LedgerSource && p.Category == ProfitCategory.OtherExpenses)
            .GroupBy(p => new { p.TransactionType, p.Detail })
            .Select(g => new { g.Key.TransactionType, g.Key.Detail, Amount = g.Sum(p => p.Amount), Lines = g.Count() })
            .OrderByDescending(g => g.Amount < 0 ? -g.Amount : g.Amount)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => new ProfitUnmappedRow(r.TransactionType, r.Detail, r.Amount, r.Lines))];
    }

    /// <summary>
    /// The marketplace's profit lines on its local days <paramref name="from"/>–<paramref name="to"/>: ledger lines by
    /// the instants those days start and end, settlement lines by their (UTC midnight) dates.
    /// </summary>
    private IQueryable<ProfitLine> InRange(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var startsAt = Application.Common.LocalTime.StartOfDay(from, zone);
        var endsBefore = Application.Common.LocalTime.StartOfDay(to.AddDays(1), zone);
        var firstDate = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var afterDate = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return dbContext.ProfitLines.AsNoTracking()
            .Where(p => p.MarketplaceId == marketplaceId
                && (p.Source == LedgerSource
                    ? p.PostedDate >= startsAt && p.PostedDate < endsBefore
                    : p.PostedDate >= firstDate && p.PostedDate < afterDate));
    }

    /// <summary>The Windows name SQL Server's <c>AT TIME ZONE</c> knows for an IANA zone.</summary>
    private static string WindowsZone(string timeZoneId) =>
        TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windows) ? windows
        : timeZoneId == "UTC" ? "UTC"
        : throw new InvalidOperationException($"Time zone '{timeZoneId}' has no Windows name for SQL Server.");

    private static string? Blank(string? sku) => string.IsNullOrWhiteSpace(sku) ? null : sku;
}
