namespace AERai.Web.Application.Inventory;

/// <summary>The result for one uploaded file or one ASIN pulled from Amazon.</summary>
/// <param name="Source">The file name (with its .zip, if any) or the ASIN.</param>
/// <param name="Skus">The SKUs it matched; empty when it matched none.</param>
/// <param name="Status">What happened.</param>
/// <param name="Detail">Why, for anything that was not stored.</param>
public sealed record PictureImportOutcome(string Source, IReadOnlyList<string> Skus, PictureImportStatus Status, string? Detail = null);
