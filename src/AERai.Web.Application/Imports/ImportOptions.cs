using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Limits applied to uploaded import files. Bound from the <c>Import</c> configuration section.
/// </summary>
public sealed class ImportOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Import";

    /// <summary>Largest accepted upload, in bytes. Defaults to 20 MB.</summary>
    [Range(1, 200 * 1024 * 1024)]
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Largest accepted number of data rows per file.</summary>
    [Range(1, 1_000_000)]
    public int MaxRows { get; set; } = 250_000;

    /// <summary>Accepted file extensions (lower-case, including the dot).</summary>
    [MinLength(1)]
    public string[] AllowedExtensions { get; set; } = [".csv", ".tsv", ".txt"];
}
