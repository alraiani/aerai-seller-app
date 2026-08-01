namespace AERai.Seller.Domain;

/// <summary>
/// Tracks the last successful sync per sync job (e.g. "Orders", "Inventory"),
/// so the Dashboard's last-sync-status widget and any scheduled sync can report/resume correctly.
/// </summary>
public class SyncMetadata
{
    public required string SyncJobName { get; set; }
    public DateTimeOffset LastSuccessfulSyncAt { get; set; }
    public bool LastSyncSucceeded { get; set; }
    public string? LastErrorMessage { get; set; }
}
