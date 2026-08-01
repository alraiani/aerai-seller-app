using System.Net;

namespace AERai.Seller.SpApiClient;

public sealed class SpApiException(string operationName, HttpStatusCode statusCode, string? responseBody)
    : Exception($"SP-API operation '{operationName}' failed with status {(int)statusCode} {statusCode}.")
{
    public string OperationName { get; } = operationName;
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}
