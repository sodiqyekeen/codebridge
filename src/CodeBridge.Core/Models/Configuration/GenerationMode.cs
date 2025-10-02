namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Defines how and when SDK generation should occur.
/// </summary>
public enum GenerationMode
{
    /// <summary>
    /// SDK must be generated manually using CLI commands.
    /// Provides full control over when generation happens.
    /// </summary>
    Manual,

    /// <summary>
    /// SDK generates automatically during build process.
    /// Integrates with MSBuild for seamless generation.
    /// </summary>
    BuildIntegration,

    /// <summary>
    /// SDK regenerates automatically when source files change.
    /// Ideal for active development with immediate feedback.
    /// </summary>
    Watch
}
