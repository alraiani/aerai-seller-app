namespace AERai.Web.Application.Ingestion;

/// <summary>Which ledger transactions to list.</summary>
/// <param name="PostedAfter">Posted on or after this instant.</param>
/// <param name="PostedBefore">Posted before this instant (Amazon allows at most 180 days after <paramref name="PostedAfter"/>).</param>
/// <param name="HeldOnly">
/// Only what Amazon still holds (<c>transactionStatus=DEFERRED</c>); otherwise no status filter.
/// </param>
public sealed record LedgerQuery(DateTimeOffset PostedAfter, DateTimeOffset PostedBefore, bool HeldOnly);
