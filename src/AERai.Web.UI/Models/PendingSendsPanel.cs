using AERai.Web.Application.Inventory;

namespace AERai.Web.UI.Models;

/// <summary>Data for the shared <c>_PendingSends</c> partial (on the Worksheet and the Pending sends page).</summary>
/// <param name="Items">Open sends, oldest first.</param>
/// <param name="CanEdit">Whether the user can add shipment ids and close or cancel sends.</param>
/// <param name="Zone">The marketplace's time zone, for dates.</param>
/// <param name="ExpiryDays">Days after which a pending send is flagged.</param>
/// <param name="Back">Where the forms return to: <c>worksheet</c>, or <see langword="null"/> for the Pending sends page.</param>
/// <param name="FamilyId">The family shown, kept on the way back.</param>
/// <param name="Filtered">Whether a search or view narrows the list (an empty list then means "no match", not "nothing pending").</param>
/// <param name="Query">The search and view, kept on the way back to the Pending sends page.</param>
public sealed record PendingSendsPanel(IReadOnlyList<PendingSendItem> Items, bool CanEdit, TimeZoneInfo Zone, int ExpiryDays, string? Back, int? FamilyId, bool Filtered = false, PendingSendFilter? Query = null)
{
    /// <summary>Units pending across the listed sends.</summary>
    public int Units => Items.Sum(i => i.Send.Pending);

    /// <summary>Sends flagged as overdue.</summary>
    public int OverdueCount => Items.Count(i => i.Overdue);

    /// <summary>A time in the marketplace's zone.</summary>
    /// <param name="instant">The time.</param>
    /// <returns>The local time.</returns>
    public DateTimeOffset Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);
}
