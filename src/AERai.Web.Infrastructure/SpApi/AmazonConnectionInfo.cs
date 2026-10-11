using AERai.Web.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// <see cref="IAmazonConnectionInfo"/> derived from <see cref="SpApiOptions"/>. Reports only whether
/// credentials are present — never their values.
/// </summary>
/// <param name="options">SP-API settings.</param>
internal sealed class AmazonConnectionInfo(IOptions<SpApiOptions> options) : IAmazonConnectionInfo
{
    /// <inheritdoc/>
    public string Mode => options.Value.Mode.ToString();

    /// <inheritdoc/>
    public bool CanRun => Problem is null;

    /// <inheritdoc/>
    public string? Problem => options.Value.Mode switch
    {
        SpApiMode.Disabled => "SP-API is disabled (SpApi:Mode).",
        SpApiMode.Live when !options.Value.HasCredentials => "SP-API credentials are not configured (SpApi:ClientId, SpApi:ClientSecret, SpApi:RefreshToken).",
        _ => null,
    };
}
