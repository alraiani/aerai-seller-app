namespace AERai.Web.Application.Products;

/// <summary>
/// Splits the Products search box into words. Every word must match the SKU, ASIN, title, or family
/// name, so "mat blue" finds a blue mat whatever the order of the words in its title.
/// </summary>
public static class ProductSearch
{
    /// <summary>At most this many words are used; the rest are ignored (keeps the query small).</summary>
    public const int MaxTerms = 5;

    /// <summary>The distinct words of a search, in order.</summary>
    /// <param name="search">The search text, or <see langword="null"/>.</param>
    /// <returns>Up to <see cref="MaxTerms"/> words, empty when the search is blank.</returns>
    public static IReadOnlyList<string> Terms(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxTerms)
                .ToList();
}
