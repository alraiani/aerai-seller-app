using AERai.Web.Domain.Alerts;

namespace AERai.Web.Application.Alerts;

/// <summary>What one refresh changes for a marketplace's alerts.</summary>
/// <param name="Raise">New alerts to open.</param>
/// <param name="Resolve">Ids of open alerts that no longer apply (or changed level).</param>
/// <param name="Messages">New messages for open alerts that still apply, keyed by id.</param>
public sealed record StockAlertChanges(IReadOnlyList<StockAlert> Raise, IReadOnlyList<long> Resolve, IReadOnlyDictionary<long, string> Messages)
{
    /// <summary>Whether there is anything to save.</summary>
    public bool IsEmpty => Raise.Count == 0 && Resolve.Count == 0 && Messages.Count == 0;
}
