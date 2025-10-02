namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Configuration for automatic project discovery.
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// Enable automatic project discovery from solution.
    /// </summary>
    public bool AutoDiscover { get; set; } = true;

    /// <summary>
    /// Project name patterns to include (glob patterns).
    /// </summary>
    public List<string> ProjectNamePatterns { get; set; } = new()
    {
        "*.Api.csproj",
        "*.WebApi.csproj",
        "*.Application.csproj",
        "*.Domain.csproj"
    };

    /// <summary>
    /// Namespace patterns to include.
    /// </summary>
    public List<string> NamespacePatterns { get; set; } = new() { "*" };

    /// <summary>
    /// Namespace patterns to exclude.
    /// </summary>
    public List<string> ExcludedNamespaces { get; set; } = new()
    {
        "*.Tests",
        "*.IntegrationTests",
        "*.UnitTests"
    };
}
