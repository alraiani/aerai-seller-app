using System.Net;

namespace AERai.Seller.SpApiClient;

public sealed class SpApiException(string operationName, HttpStatusCode statusCode, string? responseBody)
    : Exception(BuildMessage(operationName, statusCode, responseBody))
{
    private const int MaxBodyLengthInMessage = 2000;

    public string OperationName { get; } = operationName;
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;

    // Presentation (and other upstream layers) only ever see Exception.Message, not this type
    // itself — it can't reference SpApiClient. Amazon's actual error detail (e.g. which field
    // was invalid) lives in the response body, so it has to be folded into Message here or it's
    // lost by the time it reaches the UI.
    private static string BuildMessage(string operationName, HttpStatusCode statusCode, string? responseBody)
    {
        var baseMessage = $"SP-API operation '{operationName}' failed with status {(int)statusCode} {statusCode}.";
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return baseMessage;
        }

        var truncated = responseBody.Length > MaxBodyLengthInMessage
            ? responseBody[..MaxBodyLengthInMessage] + "... (truncated)"
            : responseBody;
        return $"{baseMessage} Response: {truncated}";
    }
}
