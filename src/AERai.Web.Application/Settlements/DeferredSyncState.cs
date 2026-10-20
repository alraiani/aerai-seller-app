namespace AERai.Web.Application.Settlements;

/// <summary>Whether a marketplace's deferred transactions are synced, and how recently.</summary>
/// <param name="HasSchedule">Whether a deferred transactions schedule exists for the marketplace.</param>
/// <param name="IsEnabled">Whether any such schedule is switched on.</param>
/// <param name="LastSuccessAt">When a run last finished reading Amazon's listing, or <see langword="null"/> if none has.</param>
public sealed record DeferredSyncState(bool HasSchedule, bool IsEnabled, DateTimeOffset? LastSuccessAt);
