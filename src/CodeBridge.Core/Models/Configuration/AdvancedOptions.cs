namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Advanced configuration options for SDK generation.
/// </summary>
public class AdvancedOptions
{
    /// <summary>
    /// Custom type mappings (C# type -> TypeScript type).
    /// </summary>
    public Dictionary<string, string> CustomTypeMappings { get; set; } = new();

    /// <summary>
    /// Endpoint patterns to exclude from generation (glob patterns).
    /// </summary>
    public List<string> ExcludedEndpoints { get; set; } = new();

    /// <summary>
    /// Type names to exclude from generation.
    /// </summary>
    public List<string> ExcludedTypes { get; set; } = new();

    /// <summary>
    /// Additional dependencies to include in package.json.
    /// </summary>
    public Dictionary<string, string> AdditionalDependencies { get; set; } = new();

    /// <summary>
    /// Additional dev dependencies to include in package.json.
    /// </summary>
    public Dictionary<string, string> AdditionalDevDependencies { get; set; } = new();

    /// <summary>
    /// Custom template files directory.
    /// </summary>
    public string? CustomTemplatesPath { get; set; }

    /// <summary>
    /// Maximum concurrent file operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;
}
