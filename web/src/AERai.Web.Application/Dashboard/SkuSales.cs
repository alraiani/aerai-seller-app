namespace AERai.Web.Application.Dashboard;

/// <summary>
/// One SKU's sales for the dashboard's "today" table.
/// </summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="Units">Units sold today.</param>
/// <param name="Revenue">Revenue today.</param>
public sealed record SkuSales(string Sku, string? Title, int Units, decimal Revenue);
