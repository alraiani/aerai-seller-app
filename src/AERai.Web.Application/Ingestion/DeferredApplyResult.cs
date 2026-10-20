namespace AERai.Web.Application.Ingestion;

/// <summary>What one deferred transactions pull changed.</summary>
/// <param name="Deferred">Transactions Amazon holds now.</param>
/// <param name="NewlyReleased">Transactions newly marked released by this pull.</param>
public sealed record DeferredApplyResult(int Deferred, int NewlyReleased);
