using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// A staging batch as listed on the Batches tool and the dashboard.
/// </summary>
/// <param name="Id">Batch id.</param>
/// <param name="Source">Report type.</param>
/// <param name="MarketplaceId">Marketplace the data belongs to.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="UploadedBy">Uploader's user name.</param>
/// <param name="UploadedAt">Upload time.</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="RowCount">Rows parsed.</param>
/// <param name="PromotedRowCount">Rows promoted on the last promotion.</param>
/// <param name="RejectedRowCount">Rows rejected on the last promotion.</param>
/// <param name="PromotedAt">Last promotion time, if any.</param>
/// <param name="ErrorMessage">Batch-level failure reason, if any.</param>
/// <param name="RawFilePath">Path of the original file in raw blob storage, if stored.</param>
/// <param name="RawFileSha256">SHA-256 of the original file, if stored.</param>
public sealed record ImportBatchSummary(
    long Id,
    ImportSource Source,
    string MarketplaceId,
    string FileName,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    ImportBatchStatus Status,
    int RowCount,
    int PromotedRowCount,
    int RejectedRowCount,
    DateTimeOffset? PromotedAt,
    string? ErrorMessage,
    string? RawFilePath,
    string? RawFileSha256);
