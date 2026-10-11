using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>Narrows the ledger list.</summary>
/// <param name="Sku">Exactly this SKU, or <see langword="null"/> for all.</param>
/// <param name="FamilyId">SKUs in this family, or <see langword="null"/> for all.</param>
/// <param name="Type">This kind of movement, or <see langword="null"/> for all.</param>
/// <param name="From">Movements on or after this instant.</param>
/// <param name="To">Movements before this instant.</param>
public sealed record HomeStockLedgerFilter(
    string? Sku = null,
    int? FamilyId = null,
    HomeStockMovementType? Type = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
