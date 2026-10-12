using System.Net;
using AERai.Web.Application.Email;

namespace AERai.Web.Application.Security;

/// <summary>Builds the password reset email.</summary>
public static class PasswordResetEmail
{
    /// <summary>Subject line.</summary>
    public const string Subject = "Reset your AERai Seller password";

    /// <summary>Creates the message.</summary>
    /// <param name="token">Whom the email is for.</param>
    /// <param name="resetLink">Absolute HTTPS link to the reset page, including the token.</param>
    /// <param name="validFor">How long the link works (stated in the email).</param>
    /// <returns>The email.</returns>
    public static EmailMessage Create(PasswordResetToken token, string resetLink, TimeSpan validFor)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(resetLink);

        var name = string.IsNullOrWhiteSpace(token.DisplayName) ? "there" : token.DisplayName;
        var minutes = (int)validFor.TotalMinutes;
        var lifetime = minutes % 60 == 0 ? $"{minutes / 60} hour{(minutes == 60 ? "" : "s")}" : $"{minutes} minutes";

        // Every interpolated value is HTML-encoded: the display name is user-entered, and the link
        // contains query-string characters (&) that must be escaped inside an href.
        var html = $"""
            <p>Hi {WebUtility.HtmlEncode(name)},</p>
            <p>Someone asked to reset the password for your AERai Seller account ({WebUtility.HtmlEncode(token.Email)}).</p>
            <p><a href="{WebUtility.HtmlEncode(resetLink)}">Choose a new password</a></p>
            <p>This link works once and expires in {lifetime}. If you didn't ask for this, ignore this email — your password won't change.</p>
            <p>— AERai Seller (seller.aeraigroup.com)</p>
            """;

        var text = $"""
            Hi {name},

            Someone asked to reset the password for your AERai Seller account ({token.Email}).

            Choose a new password: {resetLink}

            This link works once and expires in {lifetime}. If you didn't ask for this, ignore this email — your password won't change.

            — AERai Seller (seller.aeraigroup.com)
            """;

        return new EmailMessage(token.Email, name, Subject, html, text);
    }
}
