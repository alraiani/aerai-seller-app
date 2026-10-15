using System.Text.Json;

namespace AERai.Web.Application.Ingestion;

/// <summary>Turns the error document Amazon attaches to a failed report into one short line for the run history.</summary>
public static class FailureReason
{
    /// <summary>Longest reason kept; the run message also holds the report id and status.</summary>
    public const int MaxLength = 500;

    /// <summary>Extracts the reason from an error document.</summary>
    /// <param name="document">The document's text (usually JSON such as <c>{"errorDetails":"…"}</c>).</param>
    /// <returns>
    /// The <c>errorDetails</c> value when the document is JSON with one, otherwise the document's text;
    /// whitespace collapsed to single spaces and cut to <see cref="MaxLength"/>. <see langword="null"/> when empty.
    /// </returns>
    public static string? FromDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return null;
        }

        var text = ErrorDetails(document) ?? document;
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length == 0)
        {
            return null;
        }

        return collapsed.Length <= MaxLength ? collapsed : string.Concat(collapsed.AsSpan(0, MaxLength - 1), "…");
    }

    private static string? ErrorDetails(string document)
    {
        try
        {
            using var json = JsonDocument.Parse(document);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("errorDetails", out var details)
                && details.ValueKind == JsonValueKind.String
                    ? details.GetString()
                    : null;
        }
        catch (JsonException)
        {
            // Not JSON (or cut off by the read limit): the raw text is the reason.
            return null;
        }
    }
}
