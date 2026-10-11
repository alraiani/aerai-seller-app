namespace AERai.Web.Application.Inventory;

/// <summary>One uploaded file for a bulk picture upload: a picture or a .zip of pictures.</summary>
/// <param name="FileName">The file name as uploaded; its name without extension is the SKU or ASIN.</param>
/// <param name="Content">The file's bytes; read, not disposed. A .zip must be seekable.</param>
public sealed record PictureFile(string FileName, Stream Content);
