namespace AERai.Web.Application.Dashboard;

/// <summary>A headline number with its value in the comparison period.</summary>
/// <param name="Current">Value for the selected period.</param>
/// <param name="Previous">Value for the comparison period.</param>
public sealed record Metric(decimal Current, decimal Previous)
{
    /// <summary>Relative change (0.25 = +25%); <see langword="null"/> when there is nothing to compare against.</summary>
    public decimal? Change => Previous == 0 ? null : (Current - Previous) / Previous;
}
