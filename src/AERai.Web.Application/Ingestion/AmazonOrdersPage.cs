namespace AERai.Web.Application.Ingestion;

/// <summary>One page of the Orders API search.</summary>
/// <param name="Orders">The orders on this page.</param>
/// <param name="NextToken">Token for the next page, or <see langword="null"/> on the last page.</param>
/// <param name="RawJson">The response body exactly as Amazon sent it, kept in the raw landing zone for audit.</param>
public sealed record AmazonOrdersPage(IReadOnlyList<AmazonOrder> Orders, string? NextToken, string RawJson);
