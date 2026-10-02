using System.Text;
using AERai.Web.Application.Common;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Reads comma- or tab-delimited text (RFC 4180 quoting) into header-keyed records.
/// </summary>
/// <remarks>
/// The delimiter is detected from the header line: a tab anywhere in it means TSV (Amazon's native
/// report format), otherwise CSV. Stateless and thread-safe.
/// </remarks>
public static class DelimitedTextParser
{
    /// <summary>
    /// Parses the whole input.
    /// </summary>
    /// <param name="reader">Source text. The caller owns and disposes it.</param>
    /// <param name="maxRows">Maximum data rows accepted; more fails the parse rather than truncating silently.</param>
    /// <returns>The parsed file, or a failure when the input is empty, has duplicate headers, or exceeds <paramref name="maxRows"/>.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public static async Task<Result<ParsedFile>> ParseAsync(TextReader reader, int maxRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var headerLine = await ReadRecordTextAsync(reader, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return Result.Failure<ParsedFile>("The file is empty or has no header row.");
        }

        var delimiter = headerLine.Contains('\t', StringComparison.Ordinal) ? '\t' : ',';
        var headers = SplitFields(headerLine, delimiter).Select(NormalizeHeader).ToList();

        var duplicate = headers.Where(h => h.Length > 0).GroupBy(h => h).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return Result.Failure<ParsedFile>($"The column '{duplicate.Key}' appears more than once in the header row.");
        }

        var records = new List<ParsedRecord>();
        while (await ReadRecordTextAsync(reader, cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (records.Count == maxRows)
            {
                return Result.Failure<ParsedFile>($"The file has more than {maxRows:N0} data rows. Split it into smaller files.");
            }

            var values = SplitFields(line, delimiter);
            var fields = new Dictionary<string, string>(headers.Count, StringComparer.Ordinal);
            for (var i = 0; i < headers.Count; i++)
            {
                // Short rows are tolerated (missing trailing columns read as blank); staging keeps
                // the raw line so promotion can reject the row with a precise message if it matters.
                if (headers[i].Length > 0)
                {
                    fields[headers[i]] = i < values.Count ? values[i] : string.Empty;
                }
            }

            records.Add(new ParsedRecord(records.Count + 1, line, fields));
        }

        return Result.Success(new ParsedFile(headers, records));
    }

    /// <summary>
    /// Normalizes a header so that "Amazon Order Id", "amazon_order_id", and "amazon-order-id" all match.
    /// </summary>
    /// <param name="header">Header text as it appears in the file.</param>
    /// <returns>Lower-case, hyphen-separated header name.</returns>
    public static string NormalizeHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        var builder = new StringBuilder(header.Length);
        foreach (var c in header.Trim().Trim('﻿'))
        {
            builder.Append(c is ' ' or '_' ? '-' : char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads one logical record, joining physical lines while a quoted field is still open.
    /// </summary>
    private static async Task<string?> ReadRecordTextAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is null)
        {
            return null;
        }

        // An odd number of quote characters means a quoted field spans a line break.
        while (line.Count(c => c == '"') % 2 == 1)
        {
            var next = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (next is null)
            {
                break;
            }

            line = string.Concat(line, "\n", next);
        }

        return line;
    }

    /// <summary>
    /// Splits a record into fields, honoring double-quote escaping (<c>""</c> is a literal quote).
    /// </summary>
    private static List<string> SplitFields(string record, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < record.Length; i++)
        {
            var c = record[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < record.Length && record[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
