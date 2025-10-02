namespace CodeBridge.Core.Models.Configuration;

/// <summary>
/// JavaScript module system for generated code.
/// </summary>
public enum ModuleSystem
{
    /// <summary>
    /// ES Modules (import/export) - Modern standard.
    /// </summary>
    ESM,

    /// <summary>
    /// CommonJS (require/module.exports) - Node.js traditional.
    /// </summary>
    CommonJS
}
