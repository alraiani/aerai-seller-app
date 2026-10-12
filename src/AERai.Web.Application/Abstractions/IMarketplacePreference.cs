namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Where the signed-in user's chosen marketplace is remembered (a cookie in the web UI). It only
/// stores an id; <see cref="Marketplaces.ICurrentMarketplace"/> decides whether that id is usable.
/// </summary>
public interface IMarketplacePreference
{
    /// <summary>The remembered marketplace id, or <see langword="null"/> when none was chosen.</summary>
    /// <returns>The stored id, unvalidated.</returns>
    string? Read();

    /// <summary>Remembers a marketplace id for later requests.</summary>
    /// <param name="marketplaceId">An id already validated as an active marketplace.</param>
    void Write(string marketplaceId);
}
