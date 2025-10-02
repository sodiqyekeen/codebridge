namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// Storage mechanism for authentication tokens.
/// </summary>
public enum StorageType
{
    /// <summary>
    /// Browser localStorage (persists across sessions).
    /// </summary>
    LocalStorage,

    /// <summary>
    /// Browser sessionStorage (cleared on tab close).
    /// </summary>
    SessionStorage,

    /// <summary>
    /// Browser cookies (can be httpOnly for security).
    /// </summary>
    Cookie,

    /// <summary>
    /// In-memory storage (lost on page refresh).
    /// </summary>
    Memory
}
