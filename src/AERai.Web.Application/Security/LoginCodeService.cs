using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Security;

/// <summary>
/// Default <see cref="ILoginCodeService"/>.
/// </summary>
/// <param name="identity">Identity operations (issuing and checking codes, signing in).</param>
/// <param name="emailSender">Email delivery.</param>
/// <param name="logger">Logger. Never logs codes.</param>
public sealed partial class LoginCodeService(IIdentityService identity, IEmailSender emailSender, ILogger<LoginCodeService> logger)
    : ILoginCodeService
{
    /// <summary>How long a code works.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Minimum time between codes for one account, so the form (and its Resend button) can't flood a
    /// mailbox; a request inside it sends nothing and the earlier code stays valid.
    /// </summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Wrong guesses allowed per code before it is discarded. With a million possible codes this keeps
    /// guessing hopeless; each wrong guess also counts toward the account's normal lockout.
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>Digits in a code.</summary>
    public const int CodeLength = 6;

    /// <summary>Shown for any wrong, expired, used-up, or missing code (never says which).</summary>
    public const string InvalidCodeMessage = "That code is invalid or has expired. Check the latest email or request a new code.";

    /// <inheritdoc/>
    public async Task RequestCodeAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var code = await identity.CreateLoginCodeAsync(email.Trim(), CodeLifetime, ResendCooldown, cancellationToken).ConfigureAwait(false);
        if (code is null)
        {
            // Unknown address, locked account, or inside the cooldown: nothing is sent, and the caller can't tell.
            LogCodeNotSent();
            return;
        }

        await emailSender.SendAsync(LoginCodeEmail.Create(code, CodeLifetime), cancellationToken).ConfigureAwait(false);
        LogCodeSent(code.UserId);
    }

    /// <inheritdoc/>
    public async Task<Result> SignInWithCodeAsync(string email, string code, bool rememberMe, CancellationToken cancellationToken)
    {
        var digits = new string((code ?? string.Empty).Where(c => c is not (' ' or '-')).ToArray());
        if (string.IsNullOrWhiteSpace(email) || digits.Length != CodeLength || !digits.All(char.IsAsciiDigit))
        {
            return Result.Failure(InvalidCodeMessage);
        }

        return await identity.CodeSignInAsync(email.Trim(), digits, rememberMe, MaxAttempts, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in code sent for user {UserId}")]
    private partial void LogCodeSent(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in code requested but not sent (no eligible account, or cooldown)")]
    private partial void LogCodeNotSent();
}
