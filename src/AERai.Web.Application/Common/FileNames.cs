using System.Text;

namespace AERai.Web.Application.Common;

/// <summary>Helpers for the names of files the app hands out (downloads).</summary>
public static class FileNames
{
    /// <summary>A name made safe for a file name ("Microfiber Magic" → "microfiber-magic").</summary>
    /// <param name="name">Any text, e.g. a family name.</param>
    /// <returns>Lowercase ASCII letters and digits joined by single hyphens.</returns>
    public static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }

        return string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
