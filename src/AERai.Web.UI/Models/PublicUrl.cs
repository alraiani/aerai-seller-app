using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Models;

/// <summary>
/// Builds absolute links for emails from the configured public address (<c>App:PublicBaseUrl</c>,
/// e.g. https://seller.aeraigroup.com) instead of the request's Host header.
/// </summary>
/// <remarks>
/// Using the Host header would allow "reset poisoning": an attacker sends a forgot-password request
/// with a forged Host, and the victim receives a genuine email whose link points at the attacker's
/// site. Development falls back to the request host when no public URL is configured.
/// </remarks>
public static class PublicUrl
{
    /// <summary>Configuration key for the public base URL.</summary>
    public const string ConfigurationKey = "App:PublicBaseUrl";

    /// <summary>Absolute URL to a Razor Page.</summary>
    /// <param name="url">The page's URL helper.</param>
    /// <param name="configuration">App configuration.</param>
    /// <param name="request">Current request (scheme/host fallback in Development only).</param>
    /// <param name="page">Page path, e.g. <c>/Account/ResetPassword</c>.</param>
    /// <param name="values">Route/query values.</param>
    /// <returns>The absolute URL.</returns>
    /// <exception cref="InvalidOperationException">No public URL is configured outside Development.</exception>
    public static string Page(IUrlHelper url, IConfiguration configuration, HttpRequest request, string page, object values)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(request);

        var relative = url.Page(page, values) ?? throw new InvalidOperationException($"No route to page {page}.");

        if (Uri.TryCreate(configuration[ConfigurationKey], UriKind.Absolute, out var baseUri))
        {
            return new Uri(baseUri, relative).ToString();
        }

        var environment = request.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException($"{ConfigurationKey} must be set outside Development so emailed links can't be redirected via the Host header.");
        }

        return $"{request.Scheme}://{request.Host}{relative}";
    }
}
