using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Abstractions;

/// <summary>Renders the home-stock template as an Excel workbook that the upload accepts as-is.</summary>
public interface IHomeStockTemplateWriter
{
    /// <summary>Writes the workbook.</summary>
    /// <param name="title">Heading for the instructions sheet, e.g. "Home stock — United States".</param>
    /// <param name="rows">SKUs in the order they should appear.</param>
    /// <returns>The .xlsx file's bytes.</returns>
    byte[] Write(string title, IReadOnlyList<HomeStockTemplateRow> rows);
}
