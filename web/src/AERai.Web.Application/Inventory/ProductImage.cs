namespace AERai.Web.Application.Inventory;

/// <summary>A stored product picture, opened for serving.</summary>
/// <param name="Content">Picture bytes; the caller disposes it.</param>
/// <param name="ContentType">MIME type.</param>
public sealed record ProductImage(Stream Content, string ContentType);
