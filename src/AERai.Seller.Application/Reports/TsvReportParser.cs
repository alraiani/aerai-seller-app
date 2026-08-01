namespace AERai.Seller.Application.Reports;

/// <summary>
/// Parses a tab-separated SP-API report document (header row + data rows) into a list of
/// case-insensitive column lookups. Pure/no I/O so it's unit-testable in isolation.
/// </summary>
public static class TsvReportParser
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string tsvContent)
    {
        var lines = tsvContent.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return [];
        }

        var headers = lines[0].Split('\t');
        var rows = new List<IReadOnlyDictionary<string, string>>(lines.Length - 1);

        for (var i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split('\t');
            var row = new Dictionary<string, string>(headers.Length, StringComparer.OrdinalIgnoreCase);
            for (var col = 0; col < headers.Length && col < values.Length; col++)
            {
                row[headers[col].Trim()] = values[col].Trim();
            }
            rows.Add(row);
        }

        return rows;
    }
}
