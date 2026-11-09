namespace AERai.Web.Application.Ingestion;

/// <summary>One page of Amazon's transaction ledger.</summary>
/// <param name="Items">The transactions on this page.</param>
/// <param name="NextToken">Token for the next page, or <see langword="null"/> on the last page.</param>
/// <param name="RawJson">The response body exactly as Amazon sent it, kept in the raw landing zone for audit.</param>
public sealed record FinancialTransactionPage(IReadOnlyList<AmazonFinancialTransaction> Items, string? NextToken, string RawJson);
