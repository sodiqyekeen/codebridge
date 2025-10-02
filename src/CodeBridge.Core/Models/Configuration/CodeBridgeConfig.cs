namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Main configuration for CodeBridge SDK generation.
/// </summary>
public class CodeBridgeConfig
{
    /// <summary>
    /// Path to .NET solution file.
    /// </summary>
    public string? SolutionPath { get; set; }

    /// <summary>
    /// Explicit project paths (alternative to solution).
    /// </summary>
    public List<string> ProjectPaths { get; set; } = new();

    /// <summary>
    /// Output configuration.
    /// </summary>
    public OutputOptions Output { get; set; } = new();

    /// <summary>
    /// Target framework and language configuration.
    /// </summary>
    public TargetOptions Target { get; set; } = new();

    /// <summary>
    /// API client configuration.
    /// </summary>
    public ApiOptions Api { get; set; } = new();

    /// <summary>
    /// Feature toggles.
    /// </summary>
    public FeatureOptions Features { get; set; } = new();

    /// <summary>
    /// Project discovery configuration.
    /// </summary>
    public DiscoveryOptions Discovery { get; set; } = new();

    /// <summary>
    /// Generation mode and timing configuration.
    /// </summary>
    public GenerationOptions Generation { get; set; } = new();

    /// <summary>
    /// Advanced options.
    /// </summary>
    public AdvancedOptions Advanced { get; set; } = new();

    /// <summary>
    /// Validates the configuration and returns validation errors.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SolutionPath) && ProjectPaths.Count == 0)
        {
            errors.Add("Either SolutionPath or ProjectPaths must be specified");
        }

        if (!string.IsNullOrWhiteSpace(SolutionPath) && !File.Exists(SolutionPath))
        {
            errors.Add($"Solution file not found: {SolutionPath}");
        }

        if (string.IsNullOrWhiteSpace(Output.Path))
        {
            errors.Add("Output.Path is required");
        }

        if (string.IsNullOrWhiteSpace(Output.PackageName))
        {
            errors.Add("Output.PackageName is required");
        }

        if (!string.IsNullOrWhiteSpace(Api.BaseUrl) && !Uri.TryCreate(Api.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("Api.BaseUrl must be a valid URL");
        }

        return errors;
    }
}
