namespace AERai.Web.Application.Ingestion;

/// <summary>One page of the seller's inbound plans, most recently updated first.</summary>
/// <param name="Plans">The plans on this page.</param>
/// <param name="NextToken">Token for the next page, or <see langword="null"/> on the last page.</param>
/// <param name="RawJson">The response body exactly as Amazon sent it, kept in the raw landing zone for audit.</param>
public sealed record InboundPlanPage(IReadOnlyList<InboundPlanSummary> Plans, string? NextToken, string RawJson);
