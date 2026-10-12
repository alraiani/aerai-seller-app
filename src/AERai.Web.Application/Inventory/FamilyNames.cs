namespace AERai.Web.Application.Inventory;

/// <summary>Rules for product family names, shared by every place a family can be named.</summary>
public static class FamilyNames
{
    /// <summary>Longest family name accepted.</summary>
    public const int MaxLength = 100;

    /// <summary>Trims and collapses inner whitespace; blank means "no family".</summary>
    /// <param name="name">Name as typed.</param>
    /// <returns>The tidy name, or <see langword="null"/> when blank.</returns>
    public static string? Normalize(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Why a (normalized) name is not acceptable, or <see langword="null"/> when it is.</summary>
    /// <param name="name">Normalized name.</param>
    /// <returns>An error message, or null.</returns>
    public static string? Validate(string? name) =>
        name is null ? "Enter a family name."
        : name.Length > MaxLength ? $"Family names can be at most {MaxLength} characters."
        : null;
}
