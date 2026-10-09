using System.Globalization;
using AERai.Web.Application.Dashboard;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.UI.Models;

/// <summary>
/// Presentation helpers for the dashboard: money, deltas, relative times, and page links.
/// Formatting only — every number comes from <see cref="DashboardSnapshot"/>.
/// </summary>
public static class DashboardFormat
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Symbols for the marketplaces' currencies; "CA$" keeps Canadian dollars distinct from US.</summary>
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["USD"] = "$",
        ["CAD"] = "CA$",
        ["GBP"] = "£",
    };

    /// <summary>Money for headline tiles: whole units from 1,000 up, cents below.</summary>
    /// <param name="amount">Amount.</param>
    /// <param name="currency">ISO currency code.</param>
    /// <returns>e.g. "$12,345", "CA$842.10", or "£5.00" (unknown currencies get the code appended).</returns>
    public static string Money(decimal amount, string currency) =>
        Format(amount, currency, Math.Abs(amount) >= 1000 ? "N0" : "N2");

    /// <summary>Money with cents always shown, for unit prices and costs.</summary>
    /// <param name="amount">Amount.</param>
    /// <param name="currency">ISO currency code.</param>
    /// <returns>e.g. "CA$4.13".</returns>
    public static string Price(decimal amount, string currency) => Format(amount, currency, "N2");

    private static string Format(decimal amount, string currency, string numberFormat)
    {
        var number = Math.Abs(amount).ToString(numberFormat, Culture);
        var text = Symbols.TryGetValue(currency, out var symbol) ? symbol + number : $"{number} {currency}";
        return amount < 0 ? "-" + text : text;
    }

    /// <summary>Short money for chart bar values, where space is tight.</summary>
    /// <param name="amount">Amount.</param>
    /// <returns>e.g. "$842", "$2.6k", or "$12k" (no currency code; the chart is single-currency).</returns>
    public static string MoneyCompact(decimal amount) => Math.Abs(amount) switch
    {
        < 1000 => amount.ToString("C0", Culture),
        < 10_000 => (amount / 1000).ToString("C1", Culture) + "k",
        _ => (amount / 1000).ToString("C0", Culture) + "k",
    };

    /// <summary>CSS class for a change chip.</summary>
    /// <param name="change">Relative change, or <see langword="null"/>.</param>
    /// <returns>A delta class.</returns>
    public static string DeltaClass(decimal? change) => change switch
    {
        null => "delta-flat",
        >= 0.005m => "delta-up",
        <= -0.005m => "delta-down",
        _ => "delta-flat",
    };

    /// <summary>Text for a change chip.</summary>
    /// <param name="change">Relative change, or <see langword="null"/>.</param>
    /// <returns>e.g. "▲ 12%", "▼ 4.5%", or "—".</returns>
    public static string Delta(decimal? change)
    {
        if (change is not { } value)
        {
            return "—";
        }

        var arrow = value >= 0.005m ? "▲" : value <= -0.005m ? "▼" : "";
        var magnitude = Math.Abs(value);
        var percent = magnitude >= 0.1m ? magnitude.ToString("P0", Culture) : magnitude.ToString("P1", Culture);
        return $"{arrow} {percent}".Trim();
    }

    /// <summary>"what the comparison is" label for the selected period.</summary>
    /// <param name="period">Period.</param>
    /// <returns>e.g. "vs same time yesterday".</returns>
    public static string ComparisonLabel(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Today => "vs same time yesterday",
        DashboardPeriod.Last7Days => "vs previous 7 days",
        _ => "vs previous 30 days",
    };

    /// <summary>Short relative time.</summary>
    /// <param name="when">Instant.</param>
    /// <param name="now">Reference time.</param>
    /// <returns>e.g. "just now", "12 min ago", "3 h ago", "2 days ago".</returns>
    public static string Ago(DateTimeOffset? when, DateTimeOffset now)
    {
        if (when is not { } value)
        {
            return "never";
        }

        var elapsed = now - value;
        return elapsed.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)elapsed.TotalMinutes} min ago",
            < 48 * 60 => $"{(int)elapsed.TotalHours} h ago",
            _ => $"{(int)elapsed.TotalDays} days ago",
        };
    }

    /// <summary>Status light for the header: how fresh the sales (Orders) data is.</summary>
    /// <param name="orders">Orders sync glance, or <see langword="null"/> when Orders has never been set up.</param>
    /// <param name="now">Reference time.</param>
    /// <param name="staleAfter">Age after which data counts as stale.</param>
    /// <returns>A status-light class: live (fresh), paused (stale or never synced), or down (last run failed).</returns>
    public static string SyncLight(SyncGlance? orders, DateTimeOffset now, TimeSpan staleAfter) =>
        orders is null ? "status-paused" : Health(orders, now, staleAfter) switch
        {
            SyncHealth.Failed => "status-down",
            SyncHealth.Fresh => "status-live",
            _ => "status-paused",
        };

    /// <summary>Pill class for a sync source.</summary>
    /// <param name="glance">Sync glance.</param>
    /// <param name="now">Reference time.</param>
    /// <param name="staleAfter">Age after which data counts as stale.</param>
    /// <returns>A pill class.</returns>
    public static string SyncPill(SyncGlance glance, DateTimeOffset now, TimeSpan staleAfter) => Health(glance, now, staleAfter) switch
    {
        SyncHealth.Failed => "pill pill-bad",
        SyncHealth.Fresh => "pill pill-ok",
        _ => "pill pill-warn",
    };

    /// <summary>Pill text for a sync source.</summary>
    /// <param name="glance">Sync glance.</param>
    /// <param name="now">Reference time.</param>
    /// <param name="staleAfter">Age after which data counts as stale.</param>
    /// <returns>"Fresh", "Stale", "Failed", or "Never".</returns>
    public static string SyncPillLabel(SyncGlance glance, DateTimeOffset now, TimeSpan staleAfter) => Health(glance, now, staleAfter) switch
    {
        SyncHealth.Failed => "Failed",
        SyncHealth.Fresh => "Fresh",
        _ => glance.LastSuccessAt is null ? "Never" : "Stale",
    };

    // A failed latest run outranks freshness: the data may look recent but the next pull is broken.
    private static SyncHealth Health(SyncGlance glance, DateTimeOffset now, TimeSpan staleAfter)
    {
        ArgumentNullException.ThrowIfNull(glance);

        if (glance.LastStatus == SyncRunStatus.Failed)
        {
            return SyncHealth.Failed;
        }

        return glance.LastSuccessAt is { } success && now - success <= staleAfter ? SyncHealth.Fresh : SyncHealth.Stale;
    }

    /// <summary>Human label for a report type.</summary>
    /// <param name="type">Report type.</param>
    /// <returns>e.g. "FBA inventory".</returns>
    public static string ReportLabel(AmazonReportType type) => type switch
    {
        AmazonReportType.FbaInventory => "FBA inventory",
        AmazonReportType.FbaReservedInventory => "FBA reserved inventory",
        AmazonReportType.RestockRecommendations => "Restock recommendations",
        AmazonReportType.AwdInventory => "AWD inventory",
        _ => type.ToString(),
    };

    /// <summary>Razor Page path for an attention target.</summary>
    /// <param name="target">Target.</param>
    /// <returns>A page path.</returns>
    public static string PageFor(AttentionTarget target) => target switch
    {
        AttentionTarget.Inventory => "/Inventory/Index",
        AttentionTarget.Products => "/Products/Index",
        AttentionTarget.Batches => "/Tools/Batches/Index",
        _ => "/Tools/Schedules/Index",
    };

    /// <summary>Status light for an attention severity (same colors as the Amazon sync page).</summary>
    /// <param name="severity">Severity.</param>
    /// <returns>A status-light class.</returns>
    public static string SeverityLight(AttentionSeverity severity) => severity switch
    {
        AttentionSeverity.Critical => "status-down",
        AttentionSeverity.Warning => "status-paused",
        _ => "status-sim",
    };

    private enum SyncHealth
    {
        Fresh,
        Stale,
        Failed,
    }
}
