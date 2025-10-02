using CodeBridge.Core.Models.Configuration;

namespace CodeBridge.Core.Services;

/// <summary>
/// Loads and validates CodeBridge configuration from various sources.
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Loads configuration from the specified path or default locations.
    /// </summary>
    /// <param name="configPath">Optional explicit configuration file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded and validated configuration.</returns>
    Task<CodeBridgeConfig> LoadAsync(string? configPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration object and throws an exception if invalid.
    /// </summary>
    void Validate(CodeBridgeConfig config);
}
