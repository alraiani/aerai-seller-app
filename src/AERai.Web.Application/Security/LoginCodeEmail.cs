using System.Net;
using AERai.Web.Application.Email;

namespace AERai.Web.Application.Security;

/// <summary>Builds the sign-in code email.</summary>
public static class LoginCodeEmail
{
    /// <summary>Subject line. Deliberately leaves the code out, so it doesn't show in notification previews.</summary>
    public const string Subject = "Your AERai Seller sign-in code";

    /// <summary>Creates the message.</summary>
    /// <param name="code">The code and whom it is for.</param>
    /// <param name="validFor">How long the code works (stated in the email).</param>
    /// <returns>The email.</returns>
    public static EmailMessage Create(LoginCode code, TimeSpan validFor)
    {
        ArgumentNullException.ThrowIfNull(code);

        var name = string.IsNullOrWhiteSpace(code.DisplayName) ? "there" : code.DisplayName;
        var minutes = (int)validFor.TotalMinutes;

        // The display name is user-entered, so every interpolated value is HTML-encoded.
        var html = $"""
            <p>Hi {WebUtility.HtmlEncode(name)},</p>
            <p>Your code to sign in to AERai Seller ({WebUtility.HtmlEncode(code.Email)}) is:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px">{WebUtility.HtmlEncode(code.Code)}</p>
            <p>It works once and expires in {minutes} minutes. If you didn't try to sign in, ignore this email — nobody can get in without the code.</p>
            <p>— AERai Seller (seller.aeraigroup.com)</p>
            """;

        var text = $"""
            Hi {name},

            Your code to sign in to AERai Seller ({code.Email}) is: {code.Code}

            It works once and expires in {minutes} minutes. If you didn't try to sign in, ignore this email — nobody can get in without the code.

            — AERai Seller (seller.aeraigroup.com)
            """;

        return new EmailMessage(code.Email, name, Subject, html, text);
    }
}
