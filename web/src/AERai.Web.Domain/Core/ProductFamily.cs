namespace AERai.Web.Domain.Core;

/// <summary>
/// A user-defined group of similar products (e.g. "Yoga mats"), used to search and total inventory
/// by category. Shared by every marketplace, like <see cref="Product"/>.
/// </summary>
public sealed class ProductFamily
{
    /// <summary>Surrogate key.</summary>
    public int Id { get; set; }

    /// <summary>Display name; unique, case-insensitively.</summary>
    public required string Name { get; set; }
}
