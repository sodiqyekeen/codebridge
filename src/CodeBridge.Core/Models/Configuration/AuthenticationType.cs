namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Authentication mechanism for API requests.
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// No authentication required.
    /// </summary>
    None,

    /// <summary>
    /// Bearer token authentication (JWT).
    /// </summary>
    Bearer,

    /// <summary>
    /// API key authentication.
    /// </summary>
    ApiKey,

    /// <summary>
    /// OAuth2 authentication flow.
    /// </summary>
    OAuth2
}
