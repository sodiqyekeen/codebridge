namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Configuration for API client behavior.
/// </summary>
public class ApiOptions
{
    /// <summary>
    /// Base URL for API requests.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 30000;

    /// <summary>
    /// Authentication configuration.
    /// </summary>
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// CSRF protection configuration.
    /// </summary>
    public CsrfOptions Csrf { get; set; } = new();

    /// <summary>
    /// Whether to include credentials (cookies) in requests.
    /// </summary>
    public bool WithCredentials { get; set; } = false;
}

/// <summary>
/// Authentication configuration.
/// </summary>
public class AuthenticationOptions
{
    /// <summary>
    /// Authentication type.
    /// </summary>
    public AuthenticationType Type { get; set; } = AuthenticationType.Bearer;

    /// <summary>
    /// Token storage mechanism.
    /// </summary>
    public StorageType Storage { get; set; } = StorageType.LocalStorage;

    /// <summary>
    /// Token storage key name.
    /// </summary>
    public string TokenKey { get; set; } = "auth_token";

    /// <summary>
    /// Refresh token storage key name.
    /// </summary>
    public string? RefreshTokenKey { get; set; } = "refresh_token";

    /// <summary>
    /// Token refresh endpoint.
    /// </summary>
    public string? RefreshEndpoint { get; set; }
}

/// <summary>
/// CSRF protection configuration.
/// </summary>
public class CsrfOptions
{
    /// <summary>
    /// Whether CSRF protection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Endpoint to fetch CSRF token.
    /// </summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>
    /// HTTP header name for CSRF token.
    /// </summary>
    public string HeaderName { get; set; } = "X-CSRF-TOKEN";

    /// <summary>
    /// Cookie name for CSRF token.
    /// </summary>
    public string CookieName { get; set; } = "XSRF-TOKEN";
}
