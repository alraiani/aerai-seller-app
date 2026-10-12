using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Infrastructure.Storage;

/// <summary>
/// Raw landing-zone settings. Bound from the <c>RawStorage</c> configuration section.
/// </summary>
/// <remarks>
/// Exactly one way to reach the account is used: <c>ConnectionStrings:RawStorage</c> (Azurite
/// locally) takes precedence; otherwise <see cref="ServiceUri"/> with the app's managed identity (Azure).
/// </remarks>
public sealed class RawStorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RawStorage";

    /// <summary>Connection string name used for local/emulator access.</summary>
    public const string ConnectionStringName = "RawStorage";

    /// <summary>Blob container holding raw files.</summary>
    [Required]
    [RegularExpression("^[a-z0-9](?!.*--)[a-z0-9-]{1,61}[a-z0-9]$", ErrorMessage = "Must be a valid blob container name.")]
    public string ContainerName { get; set; } = "raw";

    /// <summary>Blob container holding product pictures (replaceable, unlike raw files).</summary>
    [Required]
    [RegularExpression("^[a-z0-9](?!.*--)[a-z0-9-]{1,61}[a-z0-9]$", ErrorMessage = "Must be a valid blob container name.")]
    public string ProductImageContainerName { get; set; } = "product-images";

    /// <summary>Blob service endpoint, e.g. <c>https://staeraisellerprod.blob.core.windows.net</c> (Azure only).</summary>
    public Uri? ServiceUri { get; set; }
}
