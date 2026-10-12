namespace AERai.Web.UI.Models;

/// <summary>Rate-limiting policy names (policies are defined in Program.cs).</summary>
public static class RateLimits
{
    /// <summary>
    /// Forgot-password submissions: 5 per 15 minutes per client IP, so the form can't be used to flood
    /// a mailbox or burn email quota. Viewing the page (GET) is not limited.
    /// </summary>
    public const string PasswordReset = "password-reset";

    /// <summary>
    /// Sign-in code requests (including Resend): 5 per 15 minutes per client IP, for the same reason as
    /// <see cref="PasswordReset"/>. Each account also has a one-minute cooldown between codes.
    /// </summary>
    public const string LoginCode = "login-code";

    /// <summary>
    /// Sign-in code checks: 20 per 15 minutes per client IP. Each code also dies after 5 wrong guesses and
    /// wrong guesses count toward the account lockout; this caps guessing across many accounts.
    /// </summary>
    public const string LoginCodeVerify = "login-code-verify";
}
