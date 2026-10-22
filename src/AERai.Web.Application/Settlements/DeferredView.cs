namespace AERai.Web.Application.Settlements;

/// <summary>Which deferred transactions the Deferred page lists.</summary>
public enum DeferredView
{
    /// <summary>What Amazon still holds.</summary>
    Deferred = 0,

    /// <summary>What Amazon held and has since released (as far back as the sync's history reaches).</summary>
    Released = 1,

    /// <summary>Both.</summary>
    All = 2,
}
