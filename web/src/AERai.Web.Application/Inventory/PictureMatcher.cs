namespace AERai.Web.Application.Inventory;

/// <summary>
/// Matches an uploaded picture's file name (without extension) to SKUs: the exact SKU first, then a
/// SKU that differs only by case, then an ASIN (every SKU listed under it).
/// </summary>
internal sealed class PictureMatcher
{
    private readonly Dictionary<string, PictureTarget> _bySku;
    private readonly Dictionary<string, List<PictureTarget>> _bySkuIgnoringCase;
    private readonly Dictionary<string, List<PictureTarget>> _byAsin;

    /// <summary>Indexes the catalog.</summary>
    /// <param name="targets">Every SKU.</param>
    public PictureMatcher(IReadOnlyList<PictureTarget> targets)
    {
        _bySku = targets.ToDictionary(t => t.Sku, StringComparer.Ordinal);
        _bySkuIgnoringCase = targets.GroupBy(t => t.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        _byAsin = targets.Where(t => !string.IsNullOrWhiteSpace(t.Asin))
            .GroupBy(t => t.Asin!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Sku, StringComparer.Ordinal).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Finds the SKUs a file name stands for.</summary>
    /// <param name="key">The file name without its extension.</param>
    /// <returns>The matched SKUs, or none with the reason.</returns>
    public (IReadOnlyList<PictureTarget> Targets, string? Problem) Match(string key)
    {
        if (_bySku.TryGetValue(key, out var exact))
        {
            return ([exact], null);
        }

        if (_bySkuIgnoringCase.TryGetValue(key, out var similar))
        {
            return similar.Count == 1
                ? (similar, null)
                : ([], $"Matches {similar.Count} SKUs that differ only in upper/lower case; name the file exactly as the SKU.");
        }

        return _byAsin.TryGetValue(key, out var listed)
            ? (listed, null)
            : ([], $"No SKU or ASIN is named '{key}'.");
    }
}
