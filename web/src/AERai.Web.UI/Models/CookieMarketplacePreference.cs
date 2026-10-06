using AERai.Web.Application.Abstractions;

namespace AERai.Web.UI.Models;

/// <summary>
/// Remembers the user's marketplace in a long-lived cookie, so each person (and browser) keeps
/// their own view and one user switching never changes what another sees.
/// </summary>
/// <param name="httpContextAccessor">Access to the current request.</param>
/// <param name="environment">Hosting environment (Development may run over plain HTTP).</param>
internal sealed class CookieMarketplacePreference(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment) : IMarketplacePreference
{
    /// <summary>Cookie name.</summary>
    public const string CookieName = "AERai.Marketplace";

    /// <inheritdoc/>
    public string? Read() => httpContextAccessor.HttpContext?.Request.Cookies[CookieName];

    /// <inheritdoc/>
    public void Write(string marketplaceId)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The marketplace can only be changed during a request.");

        context.Response.Cookies.Append(CookieName, marketplaceId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = !environment.IsDevelopment() || context.Request.IsHttps,

            // A UI preference, not tracking: it is needed for the app to show the right data.
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(365),
        });
    }
}
