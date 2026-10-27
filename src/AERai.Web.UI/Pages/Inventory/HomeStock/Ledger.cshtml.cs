using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace AERai.Web.UI.Pages.Inventory.HomeStock;

/// <summary>
/// The home-stock ledger: every unit received from the supplier, shipped to Amazon, or corrected by
/// a recount, with dates and a running balance. Everyone can read it; Operators and Admins can log
/// movements, reverse mistakes, and edit entries in place (an edit is recorded as a correction: the entry is
/// reversed and a corrected copy logged, so nothing is overwritten or deleted).
/// </summary>
/// <param name="ledger">Ledger use cases.</param>
/// <param name="items">Item service (families for the filter).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="clock">Clock (the form's default date is now).</param>
public sealed class LedgerModel(IHomeStockLedgerService ledger, IInventoryItemService items, ICurrentMarketplace currentMarketplace, TimeProvider clock) : ListPageModel
{
    /// <summary>Marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The marketplace's time zone, for showing and entering local dates.</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>The page of entries (the All entries view).</summary>
    public PagedResult<HomeStockLedgerEntry> Entries { get; private set; } = default!;

    /// <summary>One group per action, newest first (the By action view).</summary>
    public PagedResult<HomeStockLedgerAction> Actions { get; private set; } = default!;

    /// <summary>Actions listed per page in the By action view.</summary>
    public const int ActionsPerPage = 20;

    /// <summary>Grouped by action (the default) or every entry in order, from <c>?view=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public HomeStockLedgerView View { get; set; }

    /// <summary>The <c>view</c> value for links and redirects; absent for the default view.</summary>
    public string? ViewParam => View == HomeStockLedgerView.ByAction ? null : View.ToString();

    /// <summary>Exactly this SKU, from <c>?sku=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sku")]
    public string? Sku { get; set; }

    /// <summary>Family filter, from <c>?family=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>Type filter, from <c>?type=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "type")]
    public HomeStockMovementType? Type { get; set; }

    /// <summary>First local day to include, from <c>?from=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    /// <summary>Last local day to include, from <c>?to=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    /// <summary>The entry shown as an edit form, from <c>?edit=</c> (or after a failed save).</summary>
    [BindProperty(SupportsGet = true, Name = "edit")]
    public long? EditId { get; set; }

    /// <summary>The values typed into the edit form, after a save that failed; the entry's own otherwise.</summary>
    public CorrectionInput? EditValues { get; private set; }

    /// <summary>Why the last save of the edit form failed.</summary>
    public string? EditError { get; private set; }

    /// <summary>Opens the Log movement dialog on load (after a failed save, or from a link).</summary>
    [BindProperty(SupportsGet = true, Name = "log")]
    public bool OpenLog { get; set; }

    /// <summary>The Log movement form.</summary>
    [BindProperty]
    public MovementInput Input { get; set; } = new();

    /// <summary>Whether the user can log and reverse entries.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Whether any filter is applied.</summary>
    public bool IsFiltered => Sku is not null || FamilyId is not null || Type is not null || From is not null || To is not null || !string.IsNullOrWhiteSpace(Search);

    /// <summary>Movement types a user can log, with labels.</summary>
    public static IReadOnlyList<(HomeStockMovementType Type, string Label)> LoggableTypes { get; } =
    [
        (HomeStockMovementType.ReceivedFromSupplier, "Received from supplier"),
        (HomeStockMovementType.ShippedToAmazon, "Shipped to Amazon"),
        (HomeStockMovementType.CountCorrection, "Count correction (recount)"),
        (HomeStockMovementType.Other, "Other"),
    ];

    /// <summary>Filters to carry through paging.</summary>
    public IReadOnlyDictionary<string, string> PagerFilters => new Dictionary<string, string?>
    {
        ["sku"] = Sku,
        ["family"] = FamilyId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["type"] = Type?.ToString(),
        ["from"] = From?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ["to"] = To?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ["view"] = ViewParam,
    }.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value!); // Non-null: filtered.

    /// <summary>Label for a movement type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string Label(HomeStockMovementType type) => type switch
    {
        HomeStockMovementType.OpeningBalance => "Opening balance",
        HomeStockMovementType.ReceivedFromSupplier => "Received from supplier",
        HomeStockMovementType.ShippedToAmazon => "Shipped to Amazon",
        HomeStockMovementType.CountCorrection => "Count correction",
        _ => "Other",
    };

    /// <summary>Pill class for a movement type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>CSS classes.</returns>
    public static string Pill(HomeStockMovementType type) => type switch
    {
        HomeStockMovementType.ReceivedFromSupplier => "pill pill-ok",
        HomeStockMovementType.ShippedToAmazon => "pill pill-warn",
        HomeStockMovementType.OpeningBalance => "pill pill-running",
        _ => "pill pill-info",
    };

    /// <summary>An instant in the marketplace's local time.</summary>
    /// <param name="instant">The instant.</param>
    /// <returns>The same instant with the local offset.</returns>
    public DateTimeOffset Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);

    /// <summary>The current list's filters, view, search, and page, to come back to after a post or edit.</summary>
    public RouteValueDictionary ListRoute()
    {
        var route = new RouteValueDictionary(PagerFilters.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
        if (!string.IsNullOrWhiteSpace(Search))
        {
            route["q"] = Search;
        }

        if (PageNumber > 1)
        {
            route["p"] = PageNumber;
        }

        return route;
    }

    /// <summary><see cref="ListRoute"/> as text, for a form's <c>asp-all-route-data</c>.</summary>
    public IDictionary<string, string> ListRouteValues() =>
        ListRoute().ToDictionary(kv => kv.Key, kv => Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

    /// <summary>The same list with one entry open for editing.</summary>
    /// <param name="id">The entry.</param>
    /// <returns>The link.</returns>
    public string EditLink(long id)
    {
        var route = ListRoute();
        route["edit"] = id;
        return Url.Page("Ledger", route)!; // Non-null: this page always resolves.
    }

    /// <summary>The same list with no entry open for editing.</summary>
    public string CancelEditLink() => Url.Page("Ledger", ListRoute())!; // Non-null: this page always resolves.

    /// <summary>
    /// What changed in an edited entry, e.g. "edited Oct 23 by ops@… · was Received from supplier +20".
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The summary, or <see langword="null"/> for an entry never edited.</returns>
    public string? EditSummary(HomeStockLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Edit is not { } edit)
        {
            return null;
        }

        var en = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var was = new List<string>();
        if (edit.WasType != entry.Type)
        {
            was.Add(Label(edit.WasType));
        }

        if (edit.WasUnits != entry.Units)
        {
            was.Add(edit.WasUnits.ToString("+#,0;−#,0", en));
        }

        if (edit.WasNote != entry.Note)
        {
            was.Add(edit.WasNote is null ? "no note" : $"“{edit.WasNote}”");
        }

        return $"Edited {Local(edit.EditedAt).ToString("MMM d", en)} by {edit.EditedBy}" + (was.Count > 0 ? $" · was {string.Join(", ", was)}" : string.Empty);
    }

    /// <summary>
    /// A link to the All entries view with the same filters (for an action that lists only its newest entries).
    /// </summary>
    /// <returns>The link.</returns>
    public string AllEntriesLink()
    {
        var route = ListRoute();
        route["view"] = nameof(HomeStockLedgerView.Entries);
        route.Remove("p");
        return Url.Page("Ledger", route)!; // Non-null: this page always resolves.
    }

    /// <summary>What an action did, e.g. "Shipped to Amazon", "Received from supplier + Count correction", or "Undo".</summary>
    /// <param name="action">The action.</param>
    /// <returns>The label.</returns>
    public static string ActionLabel(HomeStockLedgerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action.IsUndo ? "Undo" : string.Join(" + ", action.Types.Select(Label));
    }

    /// <summary>Lists entries.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or just its section for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Input.Sku ??= Sku;
        var now = Local(clock.GetUtcNow()).DateTime;

        // Whole minutes: the date-time picker shows seconds and milliseconds otherwise.
        Input.OccurredAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Unspecified);
        return this.PageOrFragment("_LedgerBody");
    }

    /// <summary>Logs a movement.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the ledger, or the page with the dialog open and errors shown.</returns>
    public async Task<IActionResult> OnPostLogAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        await LoadMarketplaceAsync(cancellationToken);
        if (ModelState.IsValid)
        {
            DateTimeOffset? at = Input.OccurredAt is { } local ? LocalTime.ToInstant(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone) : null;
            var result = await ledger.RecordAsync(
                Marketplace.MarketplaceId,
                new HomeStockMovementInput(Input.Sku ?? string.Empty, Input.Type, Input.Quantity, at, Input.Reference, Input.Note),
                User.Identity!.Name!,
                cancellationToken);
            if (result.IsSuccess)
            {
                TempData[StatusMessage.Success] = result.Value;
                return RedirectToPage(new { sku = Sku, family = FamilyId, view = ViewParam });
            }

            ModelState.AddModelError(string.Empty, result.Error);
        }

        OpenLog = true;
        await LoadAsync(cancellationToken);
        return Page();
    }

    /// <summary>Edits an entry: it is reversed and a corrected copy logged in its place.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="correction">Its new type, units, and note.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list, or the list with the row still open and the error shown.</returns>
    public async Task<IActionResult> OnPostEditAsync(long id, CorrectionInput correction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correction);
        if (!CanEdit)
        {
            return Forbid();
        }

        await LoadMarketplaceAsync(cancellationToken);
        var result = await ledger.CorrectAsync(
            Marketplace.MarketplaceId,
            new HomeStockCorrectionInput(id, correction.Type, correction.Units, correction.Note),
            User.Identity!.Name!, // Non-null: the page requires an authenticated user.
            cancellationToken);
        if (result.IsSuccess)
        {
            TempData[StatusMessage.Success] = result.Value;
            return RedirectToPage(ListRoute());
        }

        // The Log movement form is bound on every post too; its empty fields aren't this form's errors.
        ModelState.Clear();
        EditId = id;
        EditValues = correction;
        EditError = result.Error;
        await LoadAsync(cancellationToken);
        return Page();
    }

    /// <summary>Sets the reason (movement and/or note) of the ticked entries, each recorded as a correction.</summary>
    /// <param name="ids">The ticked entries.</param>
    /// <param name="reasonType">The movement to set; empty keeps each entry's own.</param>
    /// <param name="reasonNote">The note to set; blank keeps each entry's own.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostReasonAsync(long[]? ids, HomeStockMovementType? reasonType, string? reasonNote, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        await LoadMarketplaceAsync(cancellationToken);
        var result = await ledger.CorrectReasonAsync(Marketplace.MarketplaceId, ids ?? [], reasonType, reasonNote, User.Identity!.Name!, cancellationToken); // Non-null: the page requires an authenticated user.
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return RedirectToPage(ListRoute());
        }

        var (updated, unchanged, skipped) = result.Value;
        var message = $"Reason set on {updated:N0} entr{(updated == 1 ? "y" : "ies")}"
            + (unchanged > 0 ? $"; {unchanged:N0} already had it" : string.Empty)
            + (skipped.Count > 0 ? $". {skipped.Count:N0} skipped: {string.Join(" ", skipped.Take(5))}{(skipped.Count > 5 ? " …" : string.Empty)}" : ".");
        TempData[skipped.Count > 0 && updated == 0 ? StatusMessage.Error : StatusMessage.Success] = message;
        return RedirectToPage(ListRoute());
    }

    /// <summary>Reverses an entry.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the ledger.</returns>
    public async Task<IActionResult> OnPostReverseAsync(long id, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        await LoadMarketplaceAsync(cancellationToken);
        var result = await ledger.ReverseAsync(Marketplace.MarketplaceId, id, User.Identity!.Name!, cancellationToken);
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? $"Entry #{id} reversed." : result.Error;
        return RedirectToPage(new { sku = Sku, family = FamilyId, view = ViewParam, p = PageNumber });
    }

    /// <summary>Undoes a whole action (every entry one save wrote), all or nothing.</summary>
    /// <param name="id">Any entry of the action.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the ledger.</returns>
    public async Task<IActionResult> OnPostUndoAsync(long id, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        await LoadMarketplaceAsync(cancellationToken);
        var result = await ledger.UndoActionAsync(Marketplace.MarketplaceId, id, User.Identity!.Name!, cancellationToken); // Non-null: the page requires an authenticated user.
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? result.Value : result.Error;
        return RedirectToPage(ListRoute());
    }

    private async Task LoadMarketplaceAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await LoadMarketplaceAsync(cancellationToken);
        Families = await items.ListFamiliesAsync(cancellationToken);
        var filter = new HomeStockLedgerFilter(
            string.IsNullOrWhiteSpace(Sku) ? null : Sku.Trim(),
            FamilyId,
            Type,
            From is { } from ? LocalTime.StartOfDay(from, Zone) : null,
            To is { } to ? LocalTime.StartOfDay(to.AddDays(1), Zone) : null);
        if (View == HomeStockLedgerView.Entries)
        {
            Entries = await ledger.ListAsync(Marketplace.MarketplaceId, filter, new PageRequest(PageNumber, 50, Search), cancellationToken);
        }
        else
        {
            Actions = await ledger.GetActionsAsync(Marketplace.MarketplaceId, filter, new PageRequest(PageNumber, ActionsPerPage, Search), cancellationToken);
        }
    }

    /// <summary>The fields of a row's edit form.</summary>
    public sealed class CorrectionInput
    {
        /// <summary>Kind of movement.</summary>
        public HomeStockMovementType Type { get; set; }

        /// <summary>Units (signed for a count correction or other).</summary>
        public int Units { get; set; }

        /// <summary>Note.</summary>
        public string? Note { get; set; }
    }

    /// <summary>The Log movement form fields.</summary>
    public sealed class MovementInput
    {
        /// <summary>Seller SKU.</summary>
        [Required(ErrorMessage = "Enter a SKU.")]
        [StringLength(64)]
        public string? Sku { get; set; }

        /// <summary>Kind of movement.</summary>
        public HomeStockMovementType Type { get; set; } = HomeStockMovementType.ReceivedFromSupplier;

        /// <summary>Units (or the counted total for a recount; signed for Other).</summary>
        [Range(-InventoryItemService.MaxHomeStock, InventoryItemService.MaxHomeStock)]
        public int Quantity { get; set; }

        /// <summary>When it happened, in the marketplace's local time.</summary>
        public DateTime? OccurredAt { get; set; }

        /// <summary>PO number, FBA shipment id, etc.</summary>
        [StringLength(HomeStockLedgerService.MaxReferenceLength)]
        public string? Reference { get; set; }

        /// <summary>Note.</summary>
        [StringLength(HomeStockLedgerService.MaxNoteLength)]
        public string? Note { get; set; }
    }
}
