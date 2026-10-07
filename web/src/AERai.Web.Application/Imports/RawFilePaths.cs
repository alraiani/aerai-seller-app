using System.Text;
using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Naming convention for files in the raw landing zone:
/// <c>{source}/{yyyy}/{MM}/{dd}/{id}-{safe-file-name}</c>, e.g.
/// <c>orders/2026/10/02/3f2c…-orders.csv</c>.
/// </summary>
/// <remarks>
/// Partitioning by source and UTC receive date keeps listings cheap and lets lifecycle rules
/// (cool/archive tiers) act on age. The id makes every path unique, so two uploads of the same
/// file name never collide and a stored file is never overwritten.
/// </remarks>
public static class RawFilePaths
{
    /// <summary>Longest file-name segment kept in the path.</summary>
    private const int MaxFileNameLength = 100;

    /// <summary>Builds the path for a newly received file.</summary>
    /// <param name="source">Report type.</param>
    /// <param name="receivedAt">When the file was received (converted to UTC for partitioning).</param>
    /// <param name="id">Unique id for this file.</param>
    /// <param name="fileName">Original file name; reduced to a URL- and filesystem-safe form.</param>
    /// <returns>The relative blob path.</returns>
    public static string Build(ImportSource source, DateTimeOffset receivedAt, Guid id, string fileName)
    {
        var utc = receivedAt.UtcDateTime;
        return $"{source.ToString().ToLowerInvariant()}/{utc:yyyy}/{utc:MM}/{utc:dd}/{id:N}-{SanitizeFileName(fileName)}";
    }

    /// <summary>
    /// Reduces a user-supplied file name to letters, digits, '.', '-', and '_' so it cannot inject
    /// path segments or characters that blob names and URLs treat specially.
    /// </summary>
    /// <param name="fileName">Original file name (may include a path).</param>
    /// <returns>The safe name, or <c>file</c> when nothing usable remains.</returns>
    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        var builder = new StringBuilder(Math.Min(name.Length, MaxFileNameLength));
        foreach (var c in name)
        {
            if (builder.Length == MaxFileNameLength)
            {
                break;
            }

            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        }

        var safe = builder.ToString().Trim('.');
        return safe.Length == 0 ? "file" : safe;
    }
}
