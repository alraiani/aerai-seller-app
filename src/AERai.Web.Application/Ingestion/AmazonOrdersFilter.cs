namespace AERai.Web.Application.Ingestion;

/// <summary>Which orders an Orders API search returns.</summary>
/// <remarks>
/// Amazon requires exactly one of <see cref="CreatedAfter"/> and <see cref="LastUpdatedAfter"/> on every
/// request, including every later page, so the same filter is sent with each page's token.
/// <see cref="CreatedBefore"/> only bounds a <see cref="CreatedAfter"/> search.
/// </remarks>
/// <param name="CreatedAfter">Orders placed at or after this time (history windows and backfills).</param>
/// <param name="CreatedBefore">Orders placed before this time; must be at least two minutes in the past.</param>
/// <param name="LastUpdatedAfter">Orders created or changed at or after this time (incremental syncs).</param>
public sealed record AmazonOrdersFilter(DateTimeOffset? CreatedAfter, DateTimeOffset? CreatedBefore, DateTimeOffset? LastUpdatedAfter);
