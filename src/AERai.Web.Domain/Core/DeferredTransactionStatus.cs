namespace AERai.Web.Domain.Core;

/// <summary>Where a deferred Amazon transaction stands.</summary>
public enum DeferredTransactionStatus
{
    /// <summary>Amazon is still holding the money; it is in no settlement yet.</summary>
    Deferred = 1,

    /// <summary>Amazon released the money into a settlement.</summary>
    Released = 2,
}
