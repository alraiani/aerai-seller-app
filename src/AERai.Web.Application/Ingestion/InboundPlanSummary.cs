namespace AERai.Web.Application.Ingestion;

/// <summary>One Send to Amazon inbound plan, as <c>listInboundPlans</c> lists it.</summary>
/// <param name="InboundPlanId">Amazon's plan id (e.g. <c>wf…</c>).</param>
/// <param name="Name">The plan's name in Send to Amazon.</param>
/// <param name="Status">Amazon's plan status: <c>ACTIVE</c>, <c>VOIDED</c>, or <c>SHIPPED</c>.</param>
/// <param name="CreatedAt">When the plan was created.</param>
/// <param name="LastUpdatedAt">When the plan last changed; the listing is sorted by it, newest first.</param>
/// <param name="MarketplaceIds">The marketplaces the plan ships into.</param>
public sealed record InboundPlanSummary(
    string InboundPlanId,
    string? Name,
    string Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastUpdatedAt,
    IReadOnlyList<string> MarketplaceIds);
