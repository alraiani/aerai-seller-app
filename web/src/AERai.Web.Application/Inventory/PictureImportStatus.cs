namespace AERai.Web.Application.Inventory;

/// <summary>What happened to one file or Amazon listing in a picture import.</summary>
public enum PictureImportStatus
{
    /// <summary>Stored for SKUs that had no picture.</summary>
    Added,

    /// <summary>Stored, replacing an existing picture.</summary>
    Replaced,

    /// <summary>The file name matches no SKU or ASIN.</summary>
    NoMatch,

    /// <summary>An earlier file in the same upload already set the picture for these SKUs.</summary>
    Duplicate,

    /// <summary>Not a valid JPEG, PNG, or WebP picture within the size limit.</summary>
    Rejected,

    /// <summary>Amazon's catalog has no picture for the ASIN.</summary>
    NotOnAmazon,

    /// <summary>The picture could not be downloaded or stored.</summary>
    Failed,
}
