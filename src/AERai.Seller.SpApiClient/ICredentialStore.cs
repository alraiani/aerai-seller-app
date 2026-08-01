namespace AERai.Seller.SpApiClient;

/// <summary>
/// Supplies SP-API credentials to the client layer. Implemented by the host app
/// (Infrastructure/Wpf) against wherever credentials are actually stored
/// (DPAPI-encrypted local settings) — this layer has zero knowledge of that.
/// </summary>
public interface ICredentialStore
{
    Task<SpApiCredentials?> GetCredentialsAsync(CancellationToken cancellationToken = default);
}
