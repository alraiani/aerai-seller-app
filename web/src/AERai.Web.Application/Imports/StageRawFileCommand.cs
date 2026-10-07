using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Request to parse a file that is already in the raw landing zone into a new staging batch.
/// This is the entry point for any ingestion path: browser uploads, re-staging an earlier batch,
/// and (future) automated SP-API report downloads that write straight to blob storage.
/// </summary>
/// <param name="Source">Report type the file contains.</param>
/// <param name="MarketplaceId">Marketplace the data belongs to.</param>
/// <param name="RawFilePath">Path in the raw container.</param>
/// <param name="RawFileSha256">SHA-256 of the file, recorded on the batch.</param>
/// <param name="DisplayFileName">Original file name to show in the UI.</param>
/// <param name="RequestedBy">User (or system identity) requesting the staging, for audit.</param>
public sealed record StageRawFileCommand(ImportSource Source, string MarketplaceId, string RawFilePath, string RawFileSha256, string DisplayFileName, string RequestedBy);
