namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Configuration for SDK output and npm package generation.
/// </summary>
public class OutputOptions
{
    /// <summary>
    /// Output directory path for generated SDK.
    /// </summary>
    public string Path { get; set; } = "./generated-sdk";

    /// <summary>
    /// npm package name (e.g., @myorg/api-client).
    /// </summary>
    public string PackageName { get; set; } = "api-client";

    /// <summary>
    /// Package version (semver).
    /// </summary>
    public string PackageVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Package author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Package description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Package license.
    /// </summary>
    public string License { get; set; } = "MIT";

    /// <summary>
    /// Repository URL.
    /// </summary>
    public string? Repository { get; set; }

    /// <summary>
    /// Homepage URL.
    /// </summary>
    public string? Homepage { get; set; }

    /// <summary>
    /// Package version (alternative to PackageVersion for consistency).
    /// </summary>
    public string? Version => PackageVersion;

    /// <summary>
    /// Whether to include contributing guidelines in README.
    /// </summary>
    public bool IncludeContributing { get; set; } = false;

    /// <summary>
    /// Clean output directory before generation.
    /// </summary>
    public bool CleanBeforeGenerate { get; set; } = true;
}
