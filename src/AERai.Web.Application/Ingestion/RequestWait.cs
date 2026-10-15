namespace AERai.Web.Application.Ingestion;

/// <summary>
/// A queued run whose report request will wait for Amazon's request limit
/// (see <see cref="ReportRequestCooldown"/>). An estimate made when queueing: the run decides when it starts.
/// </summary>
/// <param name="ScheduleName">The schedule's name.</param>
/// <param name="Until">When the request is expected to be sent.</param>
/// <param name="Message">The explanation shown to the user (<see cref="ReportRequestCooldown.Describe"/>).</param>
public sealed record RequestWait(string ScheduleName, DateTimeOffset Until, string Message);
