using CodeBridge.Core.Models;

namespace CodeBridge.Core.Services;

/// <summary>
/// Maps C# types to TypeScript types.
/// </summary>
public interface ITypeMapper
{
    /// <summary>
    /// Maps a C# TypeInfo to TypeScript type string.
    /// </summary>
    /// <param name="typeInfo">The C# type information.</param>
    /// <returns>The TypeScript type string.</returns>
    string MapToTypeScript(TypeInfo typeInfo);

    /// <summary>
    /// Maps a C# type name string to TypeScript type string.
    /// </summary>
    /// <param name="csharpTypeName">The C# type name.</param>
    /// <returns>The TypeScript type string.</returns>
    string MapToTypeScript(string csharpTypeName);

    /// <summary>
    /// Checks if a type is a primitive type that doesn't need interface generation.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns>True if primitive, false otherwise.</returns>
    bool IsPrimitiveType(string typeName);

    /// <summary>
    /// Registers a custom type mapping.
    /// </summary>
    /// <param name="csharpType">The C# type name.</param>
    /// <param name="typeScriptType">The TypeScript type to map to.</param>
    void RegisterCustomMapping(string csharpType, string typeScriptType);
}
