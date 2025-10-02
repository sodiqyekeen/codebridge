namespace CodeBridge.Core.Models;

/// <summary>
/// Information about a discovered .NET project.
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// Project file path (.csproj).
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Project name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Target framework(s).
    /// </summary>
    public List<string> TargetFrameworks { get; init; } = new();

    /// <summary>
    /// Project references.
    /// </summary>
    public List<string> ProjectReferences { get; init; } = new();

    /// <summary>
    /// Package references.
    /// </summary>
    public Dictionary<string, string> PackageReferences { get; init; } = new();
}
