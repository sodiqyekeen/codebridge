namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Configuration for how and when SDK generation occurs.
/// </summary>
public class GenerationOptions
{
    /// <summary>
    /// Generation mode (manual, build integration, or watch).
    /// </summary>
    public GenerationMode Mode { get; set; } = GenerationMode.Manual;

    /// <summary>
    /// Whether generation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Build events that trigger generation (for BuildIntegration mode).
    /// </summary>
    public List<BuildEvent> BuildEvents { get; set; } = new() { BuildEvent.AfterBuild };

    /// <summary>
    /// Paths to watch for changes (for Watch mode).
    /// </summary>
    public List<string> WatchPaths { get; set; } = new()
    {
        "./Controllers",
        "./Application",
        "./Domain"
    };

    /// <summary>
    /// File patterns to watch (for Watch mode).
    /// </summary>
    public List<string> WatchPatterns { get; set; } = new() { "*.cs" };

    /// <summary>
    /// Debounce delay in milliseconds before regenerating (for Watch mode).
    /// </summary>
    public int DebounceMs { get; set; } = 2000;

    /// <summary>
    /// Only validate configuration without generating (for CI/CD).
    /// </summary>
    public bool ValidateOnly { get; set; } = false;
}
