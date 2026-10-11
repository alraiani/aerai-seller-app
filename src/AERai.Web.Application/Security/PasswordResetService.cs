using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Security;

/// <summary>
/// Default <see cref="IPasswordResetService"/>.
/// </summary>
/// <param name="identity">Identity operations (tokens, password changes).</param>
/// <param name="emailSender">Email delivery.</param>
/// <param name="logger">Logger. Never logs tokens or links.</param>
public sealed partial class PasswordResetService(IIdentityService identity, IEmailSender emailSender, ILogger<PasswordResetService> logger)
    : IPasswordResetService
{
    /// <summary>How long a reset link works. Infrastructure configures Identity's token lifetime from this.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(1);

    /// <summary>Shown for any invalid, expired, or already-used link (never says which).</summary>
    public const string InvalidLinkMessage = "This reset link is invalid or has expired. Request a new one.";

    /// <inheritdoc/>
    public async Task RequestResetAsync(string email, Func<PasswordResetToken, string> buildResetLink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buildResetLink);

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var token = await identity.CreatePasswordResetTokenAsync(email.Trim(), cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            // Unknown address or admin-locked account: nothing is sent, and the caller can't tell.
            LogResetNotSent();
            return;
        }

        var message = PasswordResetEmail.Create(token, buildResetLink(token), LinkLifetime);
        await emailSender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        LogResetSent(token.UserId);
    }

    /// <inheritdoc/>
    public async Task<Result> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure(InvalidLinkMessage);
        }

        var result = await identity.ResetPasswordAsync(userId, token, newPassword, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            LogResetCompleted(userId);
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset email sent for user {UserId}")]
    private partial void LogResetSent(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for an address with no eligible account; nothing sent")]
    private partial void LogResetNotSent();

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset completed for user {UserId}")]
    private partial void LogResetCompleted(string userId);
}
