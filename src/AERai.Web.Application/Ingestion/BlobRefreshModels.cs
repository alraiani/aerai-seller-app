namespace AERai.Web.Application.Ingestion;

/// <summary>How a blob refresh should treat the files it finds.</summary>
/// <param name="DefaultMarketplaceId">Marketplace for files that do not name one in their metadata (files uploaded by hand).</param>
/// <param name="Promote">Whether to promote each staged report straight into the reports, as an auto-promote schedule does.</param>
/// <param name="Reprocess">Whether to load files that were already loaded too (rebuilding a database from its raw files).</param>
public sealed record BlobRefreshRequest(string DefaultMarketplaceId, bool Promote = true, bool Reprocess = false);

/// <summary>What a blob refresh would pick up, without doing it.</summary>
/// <param name="NewReportFiles">Report files (orders, settlements, generic inventory) not loaded yet.</param>
/// <param name="NewOrderPages">Orders API pages not loaded yet.</param>
/// <param name="AlreadyLoaded">Files that were loaded before.</param>
/// <param name="SnapshotFiles">Inventory snapshot files, which a refresh never loads because they are dated by the day they arrive.</param>
public sealed record BlobRefreshPreview(int NewReportFiles, int NewOrderPages, int AlreadyLoaded, int SnapshotFiles)
{
    /// <summary>Files a refresh would load.</summary>
    public int NewFiles => NewReportFiles + NewOrderPages;
}

/// <summary>What a blob refresh did.</summary>
/// <param name="Loaded">Files loaded into the database.</param>
/// <param name="Empty">Report files with a header but no data rows, recorded so they are not tried again.</param>
/// <param name="AlreadyLoaded">Files skipped because they were loaded before.</param>
/// <param name="SnapshotFiles">Inventory snapshot files left alone.</param>
/// <param name="OrdersWritten">Orders written from Orders API pages.</param>
/// <param name="Promoted">Staged reports promoted into the reports.</param>
/// <param name="Problems">Files that could not be loaded, each with the reason.</param>
/// <param name="HitLimit">Whether more files were waiting than one refresh handles; refresh again for the rest.</param>
public sealed record BlobRefreshSummary(
    int Loaded, int Empty, int AlreadyLoaded, int SnapshotFiles, int OrdersWritten, int Promoted, IReadOnlyList<string> Problems, bool HitLimit);
