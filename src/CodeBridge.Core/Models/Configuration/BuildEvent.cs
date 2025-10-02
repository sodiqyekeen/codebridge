namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// MSBuild events that can trigger SDK generation.
/// </summary>
public enum BuildEvent
{
    /// <summary>
    /// Generate before compilation starts.
    /// </summary>
    BeforeBuild,

    /// <summary>
    /// Generate after compilation completes.
    /// </summary>
    AfterBuild,

    /// <summary>
    /// Generate after publish operation.
    /// </summary>
    AfterPublish
}
