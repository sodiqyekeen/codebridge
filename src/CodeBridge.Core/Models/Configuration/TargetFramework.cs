namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Target frontend framework for SDK generation.
/// </summary>
public enum TargetFramework
{
    /// <summary>
    /// React framework with hooks support.
    /// </summary>
    React,

    /// <summary>
    /// Next.js framework with Server Components and API routes.
    /// </summary>
    NextJs,

    /// <summary>
    /// Vue.js framework (future support).
    /// </summary>
    Vue,

    /// <summary>
    /// Angular framework (future support).
    /// </summary>
    Angular,

    /// <summary>
    /// Plain TypeScript without framework-specific features.
    /// </summary>
    Vanilla
}
