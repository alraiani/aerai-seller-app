using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Request to load an uploaded report file into the staging schema.
/// </summary>
/// <param name="Source">The report type the file contains.</param>
/// <param name="FileName">Original file name (display only).</param>
/// <param name="Length">File size in bytes, checked against <see cref="ImportOptions.MaxFileBytes"/>.</param>
/// <param name="Content">Readable stream of the file's text. The caller owns and disposes it.</param>
/// <param name="UploadedBy">User name of the uploader, recorded for audit.</param>
public sealed record ImportFileCommand(ImportSource Source, string FileName, long Length, Stream Content, string UploadedBy);
