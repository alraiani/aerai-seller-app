namespace AERai.Web.Application.Ingestion;

/// <summary>What writing a page of Orders API orders changed.</summary>
/// <param name="Inserted">Orders that were new.</param>
/// <param name="Updated">Orders that already existed and were refreshed.</param>
public sealed record OrderUpsertResult(int Inserted, int Updated)
{
    /// <summary>Orders written in total.</summary>
    public int Total => Inserted + Updated;
}
