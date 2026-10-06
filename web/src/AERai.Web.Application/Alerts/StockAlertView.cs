using AERai.Web.Domain.Alerts;

namespace AERai.Web.Application.Alerts;

/// <summary>An alert as one user sees it.</summary>
/// <param name="Id">Alert id.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="Level">How serious it is.</param>
/// <param name="Message">Detail.</param>
/// <param name="RaisedAt">When it was raised.</param>
/// <param name="ResolvedAt">When it cleared, if it has.</param>
/// <param name="IsRead">Whether this user has read it.</param>
public sealed record StockAlertView(long Id, string Sku, string? Title, StockAlertLevel Level, string Message, DateTimeOffset RaisedAt, DateTimeOffset? ResolvedAt, bool IsRead);
