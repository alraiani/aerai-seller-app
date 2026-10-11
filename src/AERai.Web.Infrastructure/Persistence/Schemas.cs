namespace AERai.Web.Infrastructure.Persistence;

/// <summary>
/// SQL Server schema names. Each schema has one responsibility in the data flow
/// <c>stg</c> (raw) → <c>core</c> (curated) → <c>rpt</c> (views); <c>ops</c> runs ingestion; <c>auth</c> holds Identity.
/// </summary>
public static class Schemas
{
    /// <summary>Raw imported rows, kept exactly as received.</summary>
    public const string Staging = "stg";

    /// <summary>Curated, typed, constrained business tables.</summary>
    public const string Core = "core";

    /// <summary>Read-only reporting views over <see cref="Core"/>.</summary>
    public const string Reporting = "rpt";

    /// <summary>Operational tables: ingestion schedules, run history, and the ingested-report ledger.</summary>
    public const string Operations = "ops";

    /// <summary>ASP.NET Core Identity tables.</summary>
    public const string Auth = "auth";
}
