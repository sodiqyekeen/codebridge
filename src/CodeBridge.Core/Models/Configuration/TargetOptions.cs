namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Configuration for target framework and language.
/// </summary>
public class TargetOptions
{
    /// <summary>
    /// Target frontend framework.
    /// </summary>
    public TargetFramework Framework { get; set; } = TargetFramework.React;

    /// <summary>
    /// Output language (typescript or javascript).
    /// </summary>
    public string Language { get; set; } = "typescript";

    /// <summary>
    /// Module system for generated code.
    /// </summary>
    public ModuleSystem ModuleSystem { get; set; } = ModuleSystem.ESM;

    /// <summary>
    /// TypeScript version to target (e.g., "ES2022").
    /// </summary>
    public string? TypeScriptTarget { get; set; }
}
