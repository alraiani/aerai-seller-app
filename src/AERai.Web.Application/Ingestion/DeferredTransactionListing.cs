namespace AERai.Web.Application.Ingestion;

/// <summary>Which deferred transactions to list.</summary>
public enum DeferredTransactionListing
{
    /// <summary>Transactions Amazon still holds (<c>transactionStatus=DEFERRED</c>).</summary>
    StillDeferred = 1,

    /// <summary>Transactions Amazon deferred and has since released (<c>transactionStatus=DEFERRED_RELEASED</c>).</summary>
    Released = 2,
}
