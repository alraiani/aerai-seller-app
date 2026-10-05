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

    /// <summary>Money for headline tiles: whole units from 1,000 up, cents below.</summary>
    /// <param name="amount">Amount.</param>
    /// <param name="currency">ISO currency code.</param>
    /// <returns>e.g. "$12,345" or "$842.10" (non-USD amounts get the code appended).</returns>
    public static string Money(decimal amount, string currency)
    {
        var text = Math.Abs(amount) >= 1000 ? amount.ToString("C0", Culture) : amount.ToString("C2", Culture);
        return currency == "USD" ? text : $"{text} {currency}";
    }

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

    /// <summary>Health dot for a sync source.</summary>
    /// <param name="glance">Sync glance.</param>
    /// <param name="now">Reference time.</param>
    /// <param name="staleAfter">Age after which data counts as stale.</param>
    /// <returns>A dot class.</returns>
    public static string SyncDot(SyncGlance glance, DateTimeOffset now, TimeSpan staleAfter)
    {
        ArgumentNullException.ThrowIfNull(glance);

        if (glance.LastStatus == SyncRunStatus.Failed)
        {
            return "dot-bad";
        }

        return glance.LastSuccessAt is { } success && now - success <= staleAfter ? "dot-ok" : "dot-warn";
    }

    /// <summary>Human label for a report type.</summary>
    /// <param name="type">Report type.</param>
    /// <returns>e.g. "FBA inventory".</returns>
    public static string ReportLabel(AmazonReportType type) => type == AmazonReportType.FbaInventory ? "FBA inventory" : type.ToString();

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

    /// <summary>Dot class for an attention severity.</summary>
    /// <param name="severity">Severity.</param>
    /// <returns>A dot class.</returns>
    public static string SeverityDot(AttentionSeverity severity) => severity switch
    {
        AttentionSeverity.Critical => "dot-bad",
        AttentionSeverity.Warning => "dot-warn",
        _ => "dot-info",
    };
}
