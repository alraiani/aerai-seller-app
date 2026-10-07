using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Ingestion;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Tools.Schedules;

/// <summary>
/// Adds (no id), duplicates (<c>?copyFrom=</c>), or edits (with id) an ingestion schedule. Business
/// validation lives in <see cref="ISyncScheduleService"/>; this page only binds and displays.
/// </summary>
/// <param name="schedules">Schedule queries.</param>
/// <param name="scheduleService">Schedule use cases.</param>
/// <param name="identity">User list for the owner picker.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing (the default for new schedules).</param>
/// <param name="clock">Clock for relative times.</param>
public sealed class EditModel(
    ISyncScheduleRepository schedules,
    ISyncScheduleService scheduleService,
    IIdentityService identity,
    ICurrentMarketplace currentMarketplace,
    TimeProvider clock) : PageModel
{
    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Schedule id when editing; <see langword="null"/> when adding.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    /// <summary>Schedule to copy when duplicating (<c>?copyFrom=</c>).</summary>
    [BindProperty(SupportsGet = true)]
    public int? CopyFrom { get; set; }

    /// <summary>The stored schedule when editing (for the "next run / last changed" footer).</summary>
    public SyncSchedule? Existing { get; private set; }

    /// <summary>Users, for the owner picker.</summary>
    public IReadOnlyList<UserSummary> Users { get; private set; } = [];

    /// <summary>Current time, for relative times.</summary>
    public DateTimeOffset Now { get; } = clock.GetUtcNow();

    /// <summary>Active marketplaces a schedule can target.</summary>
    public IReadOnlyList<Domain.Core.Marketplace> Marketplaces { get; private set; } = [];

    /// <summary>Shows the form: blank, pre-filled for editing, or copied for duplicating.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 for an unknown id.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Users = await identity.ListUsersAsync(cancellationToken);
        var selection = await LoadMarketplacesAsync(cancellationToken);

        if (Id is { } id)
        {
            Existing = await schedules.GetAsync(id, cancellationToken);
            if (Existing is null)
            {
                return NotFound();
            }

            Input = InputModel.From(Existing);
            return Page();
        }

        if (CopyFrom is { } sourceId && await schedules.GetAsync(sourceId, cancellationToken) is { } source)
        {
            // A copy starts off so it can't double-pull alongside the original before it's adjusted.
            Input = InputModel.From(source);
            var copyName = $"Copy of {source.Name}";
            Input.Name = copyName.Length <= 100 ? copyName : copyName[..100];
            Input.IsEnabled = false;
            Input.OwnerEmail = User.Identity!.Name;
            return Page();
        }

        Input.MarketplaceId = selection.Current.MarketplaceId;
        Input.TimeZoneId = selection.Current.TimeZoneId;
        Input.OwnerEmail = User.Identity!.Name;
        return Page();
    }

    /// <summary>Saves the schedule.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the list on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayAsync(cancellationToken);
        }

        var input = Input.ToInput();
        var user = User.Identity!.Name!;
        var result = Id is { } id
            ? await scheduleService.UpdateAsync(id, input, user, cancellationToken)
            : await scheduleService.CreateAsync(input, user, cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return await RedisplayAsync(cancellationToken);
        }

        TempData[StatusMessage.Success] = Id is null ? $"Schedule '{input.Name}' added." : $"Schedule '{input.Name}' saved.";
        return RedirectToPage("Index");
    }

    /// <summary>Soft-deletes the schedule being edited.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the list.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        if (Id is not { } id)
        {
            return NotFound();
        }

        var result = await scheduleService.DeleteAsync(id, User.Identity!.Name!, cancellationToken);
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess
            ? "Schedule deleted. Its run history is kept, and it can be restored from the Deleted tab."
            : result.Error;
        return RedirectToPage("Index");
    }

    private async Task<IActionResult> RedisplayAsync(CancellationToken cancellationToken)
    {
        Users = await identity.ListUsersAsync(cancellationToken);
        await LoadMarketplacesAsync(cancellationToken);
        if (Id is { } id)
        {
            Existing = await schedules.GetAsync(id, cancellationToken);
        }

        return Page();
    }

    private async Task<MarketplaceSelection> LoadMarketplacesAsync(CancellationToken cancellationToken)
    {
        var selection = await currentMarketplace.GetAsync(cancellationToken);
        Marketplaces = [.. selection.All.Where(m => m.IsActive)];
        return selection;
    }

    /// <summary>Schedule form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Display name.</summary>
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Report to pull.</summary>
        [Display(Name = "Amazon report")]
        public AmazonReportType ReportType { get; set; } = AmazonReportType.Orders;

        /// <summary>Marketplace to pull for.</summary>
        [Required]
        [Display(Name = "Marketplace")]
        public string MarketplaceId { get; set; } = string.Empty;

        /// <summary>Interval or daily.</summary>
        [Display(Name = "Repeat")]
        public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Interval;

        /// <summary>Minutes between runs.</summary>
        [Display(Name = "Every (minutes)")]
        public int? IntervalMinutes { get; set; } = 60;

        /// <summary>Local time of day.</summary>
        [Display(Name = "At")]
        [DataType(DataType.Time)]
        public TimeOnly? DailyTime { get; set; } = new(6, 0);

        /// <summary>IANA time zone.</summary>
        [Display(Name = "Time zone")]
        public string TimeZoneId { get; set; } = "America/New_York";

        /// <summary>How far back the first run reaches.</summary>
        [Display(Name = "First-run lookback (days)")]
        [Range(1, SyncScheduleService.MaxLookbackDays)]
        public int LookbackDays { get; set; } = 7;

        /// <summary>Promote immediately.</summary>
        [Display(Name = "Send new data straight into reports")]
        public bool AutoPromote { get; set; } = true;

        /// <summary>Run automatically. New schedules start on so adding one actually syncs.</summary>
        [Display(Name = "Run automatically")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>Free-text notes.</summary>
        [StringLength(SyncScheduleService.MaxNotesLength)]
        public string? Notes { get; set; }

        /// <summary>Responsible user.</summary>
        [Display(Name = "Owner")]
        public string? OwnerEmail { get; set; }

        /// <summary>Copies a stored schedule into the form.</summary>
        /// <param name="s">The schedule.</param>
        /// <returns>The form model.</returns>
        public static InputModel From(SyncSchedule s)
        {
            ArgumentNullException.ThrowIfNull(s);
            return new InputModel
            {
                Name = s.Name,
                ReportType = s.ReportType,
                MarketplaceId = s.MarketplaceId,
                Frequency = s.Frequency,
                IntervalMinutes = s.IntervalMinutes ?? 60,
                DailyTime = s.DailyTime ?? new TimeOnly(6, 0),
                TimeZoneId = s.TimeZoneId,
                LookbackDays = s.LookbackDays,
                AutoPromote = s.AutoPromote,
                IsEnabled = s.IsEnabled,
                Notes = s.Notes,
                OwnerEmail = s.OwnerEmail,
            };
        }

        /// <summary>Converts the form to the Application input.</summary>
        /// <returns>The schedule input.</returns>
        public SyncScheduleInput ToInput() =>
            new(Name, ReportType, MarketplaceId, IsEnabled, Frequency, IntervalMinutes, DailyTime, TimeZoneId, LookbackDays, AutoPromote, Notes, OwnerEmail);
    }
}
