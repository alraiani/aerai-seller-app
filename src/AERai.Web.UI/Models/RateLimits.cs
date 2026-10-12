namespace AERai.Web.UI.Models;

/// <summary>Rate-limiting policy names (policies are defined in Program.cs).</summary>
public static class RateLimits
{
    /// <summary>
    /// Forgot-password submissions: 5 per 15 minutes per client IP, so the form can't be used to flood
    /// a mailbox or burn email quota. Viewing the page (GET) is not limited.
    /// </summary>
    public const string PasswordReset = "password-reset";
}
