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
/// Amazon still holds, and with <c>DEFERRED_RELEASED</c> what it deferred and has since released. Each comes
/// with a <c>DeferredContext</c> (the policy and the release date), the settlement the sale was made in
/// (<c>SETTLEMENT_ID</c>), and its products (each item's <c>ProductContext</c>).
/// </remarks>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class FinancesApiClient(HttpClient http, IOptions<SpApiOptions> options)
{
    private const string BasePath = "finances/2024-06-19";

    /// <summary>Amazon's status for a transaction it still holds.</summary>
    internal const string DeferredStatus = "DEFERRED";

    /// <summary>Amazon's status for a transaction it deferred and has since released.</summary>
    internal const string ReleasedStatus = "DEFERRED_RELEASED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Calls <c>listTransactions</c> for one page of still-deferred or released transactions.</summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="listing">Still held, or released.</param>
    /// <param name="postedAfter">Only transactions posted on or after this instant.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, parsed and as raw JSON.</returns>
    public async Task<DeferredTransactionPage> ListDeferredAsync(
        Marketplace marketplace, DeferredTransactionListing listing, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        var status = StatusOf(listing);

        // Amazon wants the same arguments on every page, with the token added.
        var query = string.Create(CultureInfo.InvariantCulture,
            $"postedAfter={Uri.EscapeDataString(postedAfter.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))}&marketplaceId={Uri.EscapeDataString(marketplace.MarketplaceId)}&transactionStatus={status}");
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

        return ParsePage(body, listing);
    }

    /// <summary>Amazon's <c>transactionStatus</c> for a listing.</summary>
    /// <param name="listing">Listing.</param>
    /// <returns><c>DEFERRED</c> or <c>DEFERRED_RELEASED</c>.</returns>
    internal static string StatusOf(DeferredTransactionListing listing) =>
        listing == DeferredTransactionListing.Released ? ReleasedStatus : DeferredStatus;

    /// <summary>Parses a <c>listTransactions</c> response body, keeping only transactions with the listing's status.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="listing">Which listing it answers.</param>
    /// <returns>The page.</returns>
    /// <exception cref="InvalidOperationException">The body is empty or not a listing.</exception>
    public static DeferredTransactionPage ParsePage(string json, DeferredTransactionListing listing)
    {
        var status = StatusOf(listing);
        var response = JsonSerializer.Deserialize<ListTransactionsResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.ListTransactions} returned an empty body.");
        var items = (response.Payload?.Transactions ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.TransactionId)
                && (t.TransactionStatus is null || string.Equals(t.TransactionStatus, status, StringComparison.OrdinalIgnoreCase)))
            .Select(t =>
            {
                // The deferred context is the one that carries a policy or a release date; the others describe products or payments.
                var deferral = (t.Contexts ?? []).FirstOrDefault(c => c.DeferralReason is not null || c.MaturityDate is not null);
                return new AmazonDeferredTransaction(
                    t.TransactionId!.Trim(), // Non-null: transactions without an id were filtered out just above.
                    Related(t, "ORDER_ID"),
                    Related(t, "SETTLEMENT_ID"),
                    t.TransactionType,
                    t.Description,
                    t.PostedDate,
                    t.TotalAmount?.CurrencyAmount ?? 0m,
                    t.TotalAmount?.CurrencyCode,
                    deferral?.DeferralReason,
                    deferral?.MaturityDate,
                    [.. (t.Items ?? []).Select(ProductOf)]);
            })
            .ToList();
        return new DeferredTransactionPage(items, response.Payload?.NextToken, json);
    }

    /// <summary>A transaction item's product: identity from its <c>ProductContext</c>, the price from its <c>ProductCharges</c> breakdown.</summary>
    private static AmazonDeferredProduct ProductOf(Item item)
    {
        var product = (item.Contexts ?? []).FirstOrDefault(c => c.Sku is not null || c.Asin is not null);
        var principal = (item.Breakdowns ?? [])
            .FirstOrDefault(b => string.Equals(b.BreakdownType, "ProductCharges", StringComparison.OrdinalIgnoreCase))
            ?.BreakdownAmount?.CurrencyAmount ?? 0m;
        return new AmazonDeferredProduct(product?.Sku?.Trim(), product?.Asin?.Trim(), product?.QuantityShipped ?? 0, principal, item.TotalAmount?.CurrencyAmount ?? 0m);
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
        IReadOnlyList<TransactionContext>? Contexts,
        IReadOnlyList<Item>? Items);

    private sealed record Item(Money? TotalAmount, IReadOnlyList<Breakdown>? Breakdowns, IReadOnlyList<TransactionContext>? Contexts);

    private sealed record Breakdown(string? BreakdownType, Money? BreakdownAmount);

    private sealed record Money(string? CurrencyCode, decimal? CurrencyAmount);

    private sealed record RelatedIdentifier(string? RelatedIdentifierName, string? RelatedIdentifierValue);

    // One shape for every context type: the deferred context fills the first two, the product context the rest.
    private sealed record TransactionContext(
        string? ContextType, string? DeferralReason, DateTimeOffset? MaturityDate, string? Sku, string? Asin, int? QuantityShipped);
}
