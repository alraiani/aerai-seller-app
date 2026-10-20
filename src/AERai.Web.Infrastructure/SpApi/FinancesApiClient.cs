using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Finances API (version 2024-06-19), used for deferred transactions. Every call
/// is tagged with its operation name and region and sent through <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <remarks>
/// <c>listTransactions</c> is a plain paginated GET. With <c>transactionStatus=DEFERRED</c> it returns only what
/// Amazon still holds, each with a <c>DeferredContext</c> giving the policy and the release (maturity) date.
/// </remarks>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class FinancesApiClient(HttpClient http, IOptions<SpApiOptions> options)
{
    private const string BasePath = "finances/2024-06-19";

    /// <summary>Amazon's status for a transaction it still holds.</summary>
    internal const string DeferredStatus = "DEFERRED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Calls <c>listTransactions</c> for one page of still-deferred transactions.</summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="postedAfter">Only transactions posted on or after this instant.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, parsed and as raw JSON.</returns>
    public async Task<DeferredTransactionPage> ListDeferredAsync(Marketplace marketplace, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        // Amazon wants the same arguments on every page, with the token added.
        var query = string.Create(CultureInfo.InvariantCulture,
            $"postedAfter={Uri.EscapeDataString(postedAfter.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))}&marketplaceId={Uri.EscapeDataString(marketplace.MarketplaceId)}&transactionStatus={DeferredStatus}");
        if (!string.IsNullOrEmpty(nextToken))
        {
            query += $"&nextToken={Uri.EscapeDataString(nextToken)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.Value.EndpointFor(marketplace.Region), $"{BasePath}/transactions?{query}"));
        request.Options.Set(SpApiOperation.RegionKey, marketplace.Region);
        request.Options.Set(SpApiOperation.OptionKey, SpApiOperation.ListTransactions);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // SP-API error bodies ({"errors":[{code,message}]}) contain no secrets and help diagnose the call.
            throw new HttpRequestException(
                $"SP-API {SpApiOperation.ListTransactions} failed with HTTP {(int)response.StatusCode}: {(body.Length <= 500 ? body : body[..500])}",
                null,
                response.StatusCode);
        }

        return ParsePage(body);
    }

    /// <summary>Parses a <c>listTransactions</c> response body, keeping only still-deferred transactions.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The page.</returns>
    /// <exception cref="InvalidOperationException">The body is empty or not a listing.</exception>
    public static DeferredTransactionPage ParsePage(string json)
    {
        var response = JsonSerializer.Deserialize<ListTransactionsResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.ListTransactions} returned an empty body.");
        var items = (response.Payload?.Transactions ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.TransactionId)
                && (t.TransactionStatus is null || string.Equals(t.TransactionStatus, DeferredStatus, StringComparison.OrdinalIgnoreCase)))
            .Select(t =>
            {
                // The deferred context is the one that carries a policy or a release date; the others describe products or payments.
                var deferral = (t.Contexts ?? []).FirstOrDefault(c => c.DeferralReason is not null || c.MaturityDate is not null);
                return new DeferredTransactionItem(
                    t.TransactionId!.Trim(), // Non-null: transactions without an id were filtered out just above.
                    Related(t, "ORDER_ID"),
                    t.TransactionType,
                    t.Description,
                    t.PostedDate,
                    t.TotalAmount?.CurrencyAmount ?? 0m,
                    t.TotalAmount?.CurrencyCode,
                    deferral?.DeferralReason,
                    deferral?.MaturityDate);
            })
            .ToList();
        return new DeferredTransactionPage(items, response.Payload?.NextToken, json);
    }

    private static string? Related(Transaction transaction, string name) =>
        (transaction.RelatedIdentifiers ?? [])
            .FirstOrDefault(r => string.Equals(r.RelatedIdentifierName, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(r.RelatedIdentifierValue))
            ?.RelatedIdentifierValue?.Trim();

    private sealed record ListTransactionsResponse(TransactionsPayload? Payload);

    private sealed record TransactionsPayload(string? NextToken, IReadOnlyList<Transaction>? Transactions);

    private sealed record Transaction(
        string? TransactionId,
        string? TransactionType,
        string? TransactionStatus,
        string? Description,
        DateTimeOffset? PostedDate,
        Money? TotalAmount,
        IReadOnlyList<RelatedIdentifier>? RelatedIdentifiers,
        IReadOnlyList<TransactionContext>? Contexts);

    private sealed record Money(string? CurrencyCode, decimal? CurrencyAmount);

    private sealed record RelatedIdentifier(string? RelatedIdentifierName, string? RelatedIdentifierValue);

    private sealed record TransactionContext(string? ContextType, string? DeferralReason, DateTimeOffset? MaturityDate);
}
