namespace AERai.Web.Application.Dashboard;

/// <summary>Whether chart buckets are hours (Today) or days.</summary>
public enum ChartGranularity
{
    /// <summary>One bar per local hour.</summary>
    Hour = 1,

    /// <summary>One bar per local calendar day.</summary>
    Day = 2,
}

/// <summary>One bar of the sales chart.</summary>
/// <param name="LocalStart">Start of the bucket in the business time zone.</param>
/// <param name="Revenue">Revenue in the bucket.</param>
/// <param name="Units">Units sold in the bucket.</param>
/// <param name="Orders">Distinct orders in the bucket.</param>
/// <param name="IsFuture">Whether the bucket hasn't started yet (later hours of today).</param>
public sealed record ChartPoint(DateTimeOffset LocalStart, decimal Revenue, int Units, int Orders, bool IsFuture);
