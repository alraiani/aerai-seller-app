using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Products;

/// <summary>Default <see cref="IProductCostTemplateService"/>.</summary>
/// <param name="repository">Product persistence.</param>
/// <param name="writer">Workbook renderer.</param>
/// <param name="clock">Clock (dates the file name).</param>
public sealed class ProductCostTemplateService(IProductRepository repository, IProductCostTemplateWriter writer, TimeProvider clock) : IProductCostTemplateService
{
    /// <inheritdoc/>
    public async Task<(string FileName, byte[] Content)> CreateAsync(Marketplace marketplace, int? familyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var rows = await repository.ListForCostSheetAsync(marketplace.MarketplaceId, familyId, cancellationToken).ConfigureAwait(false);

        var family = familyId is null || rows.Count == 0 ? null : rows[0].Family;
        var title = family is null
            ? $"Cost of goods — {marketplace.Name} ({marketplace.Currency})"
            : $"Cost of goods — {family}, {marketplace.Name} ({marketplace.Currency})";
        var today = LocalTime.DateOf(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId));
        var fileName = string.Create(CultureInfo.InvariantCulture, $"cogs-{marketplace.Code}{(family is null ? "" : "-" + FileNames.Slug(family))}-{today:yyyy-MM-dd}.xlsx");
        return (fileName, writer.Write(title, marketplace.Currency, rows));
    }
}
