using AERai.Web.Application.Products;

namespace AERai.Web.Application.Abstractions;

/// <summary>Renders the cost-of-goods sheet as an Excel workbook that the cost upload accepts as-is.</summary>
public interface IProductCostTemplateWriter
{
    /// <summary>Writes the workbook.</summary>
    /// <param name="title">Heading for the instructions sheet, e.g. "Cost of goods — United States (USD)".</param>
    /// <param name="currency">ISO 4217 currency the costs are in, shown in the cost column's note.</param>
    /// <param name="rows">SKUs in the order they should appear.</param>
    /// <returns>The .xlsx file's bytes.</returns>
    byte[] Write(string title, string currency, IReadOnlyList<ProductCostTemplateRow> rows);
}
