using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Tools.Schedules;

/// <summary>
/// Creates (no id) or edits (with id) an ingestion schedule. Business validation lives in
/// <see cref="ISyncScheduleService"/>; this page only binds and displays.
/// </summary>
/// <param name="schedules">Schedule queries.</param>
/// <param name="scheduleService">Schedule use cases.</param>
public sealed class EditModel(ISyncScheduleRepository schedules, ISyncScheduleService scheduleService) : PageModel
{
    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Schedule id when editing; <see langword="null"/> when creating.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    /// <summary>Shows the form, pre-filled when editing.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 for an unknown id.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (Id is not { } id)
        {
            return Page();
        }

        var schedule = await schedules.GetAsync(id, cancellationToken);
        if (schedule is null)
        {
            return NotFound();
        }

        Input = InputModel.From(schedule);
        return Page();
    }

    /// <summary>Saves the schedule.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the list on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var input = Input.ToInput();
        var user = User.Identity!.Name!;
        var result = Id is { } id
            ? await scheduleService.UpdateAsync(id, input, user, cancellationToken)
            : await scheduleService.CreateAsync(input, user, cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = $"Schedule '{input.Name}' saved.";
        return RedirectToPage("Index");
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

        /// <summary>Interval or daily.</summary>
        [Display(Name = "Repeat")]
        public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Interval;

        /// <summary>Minutes between runs.</summary>
        [Display(Name = "Every (minutes)")]
        public int? IntervalMinutes { get; set; } = 60;

        /// <summary>Local time of day.</summary>
        [Display(Name = "At (local time)")]
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
        [Display(Name = "Auto-promote into reports")]
        public bool AutoPromote { get; set; } = true;

        /// <summary>Run automatically.</summary>
        [Display(Name = "Enabled")]
        public bool IsEnabled { get; set; }

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
                Frequency = s.Frequency,
                IntervalMinutes = s.IntervalMinutes ?? 60,
                DailyTime = s.DailyTime ?? new TimeOnly(6, 0),
                TimeZoneId = s.TimeZoneId,
                LookbackDays = s.LookbackDays,
                AutoPromote = s.AutoPromote,
                IsEnabled = s.IsEnabled,
            };
        }

        /// <summary>Converts the form to the Application input.</summary>
        /// <returns>The schedule input.</returns>
        public SyncScheduleInput ToInput() =>
            new(Name, ReportType, IsEnabled, Frequency, IntervalMinutes, DailyTime, TimeZoneId, LookbackDays, AutoPromote);
    }
}
