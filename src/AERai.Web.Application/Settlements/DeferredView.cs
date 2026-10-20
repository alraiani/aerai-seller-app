namespace AERai.Web.Application.Settlements;

/// <summary>Which deferred transactions the Deferred page lists.</summary>
public enum DeferredView
{
    /// <summary>What Amazon still holds.</summary>
    Deferred = 0,

    /// <summary>What Amazon released recently (within <see cref="DeferredPaymentService.ReleasedWindow"/>).</summary>
    Released = 1,
}
