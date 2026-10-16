namespace AERai.Web.Application.Orders;

/// <summary>A CSV download of the Orders list.</summary>
/// <param name="FileName">Suggested file name.</param>
/// <param name="Content">The CSV text.</param>
/// <param name="Truncated">Whether more rows matched than <see cref="OrderService.MaxExportRows"/> and the rest were left out.</param>
public sealed record OrderExport(string FileName, string Content, bool Truncated);
