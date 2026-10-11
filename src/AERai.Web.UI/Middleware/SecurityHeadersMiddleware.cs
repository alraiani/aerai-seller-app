namespace AERai.Web.UI.Middleware;

/// <summary>
/// Adds baseline security headers to every response.
/// </summary>
/// <remarks>
/// The Content-Security-Policy allows only same-origin resources and no inline script or style, so
/// pages must not use <c>style=""</c> attributes or inline <c>&lt;script&gt;</c> blocks. All front-end
/// libraries are served locally from <c>wwwroot/lib</c> for the same reason.
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
        "font-src 'self'; connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";

    /// <summary>Sets the headers, then invokes the rest of the pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        return next(context);
    }
}
