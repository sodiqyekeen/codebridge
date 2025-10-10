using System.Text;
using System.Text.RegularExpressions;
using CodeBridge.Core.Models;
using CodeBridge.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBridge.Core.Services;

/// <summary>
/// Maps C# types to TypeScript types with support for generics, collections, and custom mappings.
/// </summary>
public sealed class TypeMapper(ILogger<TypeMapper> logger, AdvancedOptions? advancedOptions = null) : ITypeMapper
{
    private readonly ILogger<TypeMapper> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<string, string> _primitiveTypeMappings = new Dictionary<string, string>
        {
            // Basic C# types
            { "string", "string" },
            { "int", "number" },
            { "long", "number" },
            { "decimal", "number" },
            { "double", "number" },
            { "float", "number" },
            { "short", "number" },
            { "byte", "number" },
            { "sbyte", "number" },
            { "uint", "number" },
            { "ulong", "number" },
            { "ushort", "number" },
            { "bool", "boolean" },
            { "boolean", "boolean" },
            { "char", "string" },
            { "object", "any" },
            { "void", "void" },
            { "dynamic", "any" },
            
            // .NET Framework types
            { "System.String", "string" },
            { "System.Int32", "number" },
            { "System.Int64", "number" },
            { "System.Decimal", "number" },
            { "System.Double", "number" },
            { "System.Single", "number" },
            { "System.Int16", "number" },
            { "System.Byte", "number" },
            { "System.SByte", "number" },
            { "System.UInt32", "number" },
            { "System.UInt64", "number" },
            { "System.UInt16", "number" },
            { "System.Boolean", "boolean" },
            { "System.Char", "string" },
            { "System.Object", "any" },
            
            // Date and Time types (mapped to string for ISO 8601 format)
            { "DateTime", "string" },
            { "DateTimeOffset", "string" },
            { "DateOnly", "string" },
            { "TimeOnly", "string" },
            { "TimeSpan", "string" },
            { "System.DateTime", "string" },
            { "System.DateTimeOffset", "string" },
            { "System.DateOnly", "string" },
            { "System.TimeOnly", "string" },
            { "System.TimeSpan", "string" },
            
            // Guid
            { "Guid", "string" },
            { "System.Guid", "string" },
            
            // Special types
            { "Type", "string" },
            { "System.Type", "string" },
            
            // Result types (unwrapped by logic below)
            { "Result", "Result<void>" },
            { "IResult", "Result<void>" },
            
            // File upload types (mapped to FormData for client-side)
            { "IFormFile", "FormData" },
            { "Microsoft.AspNetCore.Http.IFormFile", "FormData" },
            { "IFormFileCollection", "FormData" },
            { "Microsoft.AspNetCore.Http.IFormFileCollection", "FormData" },
            
            // Stream types (for file downloads)
            { "Stream", "Blob" },
            { "System.IO.Stream", "Blob" },
            { "FileStream", "Blob" },
            { "System.IO.FileStream", "Blob" },
            
            // Dictionary types (non-generic fallback)
            { "Dictionary", "Record<string, any>" },
            { "System.Collections.Generic.Dictionary", "Record<string, any>" },
            { "IDictionary", "Record<string, any>" },
            { "System.Collections.Generic.IDictionary", "Record<string, any>" },
            { "IReadOnlyDictionary", "Record<string, any>" },
            { "System.Collections.Generic.IReadOnlyDictionary", "Record<string, any>" },
            
            // Collection types (non-generic fallback)
            { "IReadOnlyList", "any[]" },
            { "System.Collections.Generic.IReadOnlyList", "any[]" },
            { "IReadOnlyCollection", "any[]" },
            { "System.Collections.Generic.IReadOnlyCollection", "any[]" },
            { "ICollection", "any[]" },
            { "System.Collections.Generic.ICollection", "any[]" },
            { "List", "any[]" },
            { "System.Collections.Generic.List", "any[]" },
            { "IList", "any[]" },
            { "System.Collections.Generic.IList", "any[]" },
            { "IEnumerable", "any[]" },
            { "System.Collections.Generic.IEnumerable", "any[]" },
            
            // Nullable (non-generic fallback - will be unwrapped by logic)
            { "Nullable", "any" },
            { "System.Nullable", "any" },
            
            // Common entity types that shouldn't be in SDK (map to any to avoid errors)
            { "Organization", "any" },
            { "User", "any" },
            { "Role", "any" },
            { "Permission", "any" },
            { "OrganizationUser", "any" },
            { "TwoFactorSetup", "any" },
            { "BackupCode", "any" },
            { "RefreshToken", "any" },
            { "OrganizationUserRole", "any" },
            { "RolePermission", "any" },
            { "PasswordResetAttempt", "any" },
            { "OrganizationInvitation", "any" },
            { "JobExecution", "any" },
            { "Notification", "any" },
            { "SignalRConnection", "any" },
            { "AuditLog", "any" },
            { "TenantAccessor", "any" },
            { "TenantEntityBase", "any" },
            { "TenantSettings", "any" },
            
            // Error types
            { "ApiError", "any" },
            { "ErrorType", "string" }, // Enum values: Validation, NotFound, Conflict, Unauthorized, Forbidden, InternalError, ServiceUnavailable, Unknown
            { "ValidationException", "any" }
        };
    private readonly Dictionary<string, string> _customTypeMappings = advancedOptions?.CustomTypeMappings ?? new Dictionary<string, string>();

    /// <summary>
    /// Maps a C# type to its TypeScript equivalent.
    /// Handles enums, collections, generics, and custom mappings.
    /// </summary>
    /// <param name="typeInfo">The type information to map.</param>
    /// <returns>TypeScript type representation as a string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when typeInfo is null.</exception>
    public string MapToTypeScript(TypeInfo typeInfo)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        // Check custom mappings first
        if (_customTypeMappings.TryGetValue(typeInfo.Name, out var customMapping))
        {
            return ApplyNullability(customMapping, typeInfo.IsNullable);
        }

        // Check primitive type mappings (includes manually mapped enums like ErrorType)
        if (_primitiveTypeMappings.TryGetValue(typeInfo.Name, out var primitiveMapping))
        {
            return ApplyNullability(primitiveMapping, typeInfo.IsNullable);
        }

        if (_primitiveTypeMappings.TryGetValue(typeInfo.FullName, out var fullPrimitiveMapping))
        {
            return ApplyNullability(fullPrimitiveMapping, typeInfo.IsNullable);
        }

        // Handle enums (only if not already mapped above)
        if (typeInfo.IsEnum)
        {
            return ApplyNullability(typeInfo.Name, typeInfo.IsNullable);
        }

        // Handle generic types
        if (typeInfo.IsGeneric)
        {
            // If GenericArguments are populated, use structured mapping
            if (typeInfo.GenericArguments.Count > 0)
            {
                var mappedType = MapGenericType(typeInfo);
                return ApplyNullability(mappedType, typeInfo.IsNullable);
            }

            // Otherwise, fall back to string-based parsing (e.g., for types discovered from .Produces<T>() metadata)
            // This handles cases like "Result<InitiatePasswordResetResponse>" where we have the full string but not parsed GenericArguments
            if (typeInfo.Name.Contains('<') && typeInfo.Name.Contains('>'))
            {
                Console.WriteLine($"[TYPEMAPPER] Input: {typeInfo.Name}");
                var mappedType = MapGenericTypeString(typeInfo.Name);
                Console.WriteLine($"[TYPEMAPPER] Output: {mappedType}");
                return ApplyNullability(mappedType, typeInfo.IsNullable);
            }
        }

        // Default to the type name for custom DTOs
        return ApplyNullability(typeInfo.Name, typeInfo.IsNullable);
    }

    /// <summary>
    /// Maps a C# type name to its TypeScript equivalent.
    /// Handles arrays, nullables, collections, and generic types.
    /// </summary>
    /// <param name="csharpTypeName">The C# type name to map.</param>
    /// <returns>TypeScript type representation as a string.</returns>
    /// <remarks>
    /// This overload accepts a string type name and performs string-based analysis
    /// to handle complex generic types and collections.
    /// </remarks>
    public string MapToTypeScript(string csharpTypeName)
    {
        if (string.IsNullOrEmpty(csharpTypeName))
            return "any";

        // Check custom mappings first
        if (_customTypeMappings.TryGetValue(csharpTypeName, out var customMapping))
        {
            return customMapping;
        }

        var isNullable = false;

        // Handle nullable notation (string?)
        if (csharpTypeName.EndsWith('?'))
        {
            isNullable = true;
            csharpTypeName = csharpTypeName.TrimEnd('?');
        }

        // Handle arrays (string[])
        if (csharpTypeName.EndsWith("[]"))
        {
            var elementType = csharpTypeName.Substring(0, csharpTypeName.Length - 2);
            var mappedElementType = MapToTypeScript(elementType);
            return ApplyNullability($"{mappedElementType}[]", isNullable);
        }

        // Handle generic types (List<string>, Result<T>, etc.)
        if (csharpTypeName.Contains('<') && csharpTypeName.Contains('>'))
        {
            var mappedType = MapGenericTypeString(csharpTypeName);
            return ApplyNullability(mappedType, isNullable);
        }

        // Handle primitive types
        if (_primitiveTypeMappings.TryGetValue(csharpTypeName, out var primitiveMapping))
        {
            return ApplyNullability(primitiveMapping, isNullable);
        }

        // Default to the type name for custom types
        return ApplyNullability(csharpTypeName, isNullable);
    }

    /// <summary>
    /// Determines if a C# type is a primitive type that maps to TypeScript built-in types.
    /// </summary>
    /// <param name="typeName">The C# type name to check.</param>
    /// <returns>True if the type is primitive; otherwise, false.</returns>
    /// <remarks>
    /// Primitive types include: string, int, bool, decimal, DateTime, Guid, etc.
    /// </remarks>
    public bool IsPrimitiveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;

        // Remove nullable marker and array notation
        typeName = typeName.TrimEnd('?').TrimEnd('[', ']');

        // Check if it's in primitive mappings
        if (_primitiveTypeMappings.ContainsKey(typeName))
            return true;

        // Check for generic wrapper types
        if (typeName.Contains('<'))
        {
            var baseType = typeName.Substring(0, typeName.IndexOf('<'));
            return baseType is "List" or "IList" or "IEnumerable" or "ICollection" or
                   "Dictionary" or "IDictionary" or "HashSet" or "ISet" or
                   "Task" or "Result" or "Nullable";
        }

        return false;
    }

    /// <summary>
    /// Registers a custom type mapping from C# type to TypeScript type.
    /// </summary>
    /// <param name="csharpType">The C# type name.</param>
    /// <param name="typeScriptType">The corresponding TypeScript type.</param>
    /// <remarks>
    /// Custom mappings take precedence over default primitive mappings.
    /// Use this to override default behavior for specific types.
    /// </remarks>
    public void RegisterCustomMapping(string csharpType, string typeScriptType)
    {
        _customTypeMappings[csharpType] = typeScriptType;
        _logger.LogDebug("Registered custom type mapping: {CSharpType} -> {TypeScriptType}",
            csharpType, typeScriptType);
    }

    #region Private Helper Methods

    private string MapGenericType(TypeInfo typeInfo)
    {
        var typeName = typeInfo.Name;

        // Handle collections: List<T>, IEnumerable<T>, etc.
        if (IsCollectionType(typeName))
        {
            if (typeInfo.GenericArguments.Count > 0)
            {
                var elementType = MapToTypeScript(typeInfo.GenericArguments[0]);
                return $"{elementType}[]";
            }
            return "any[]";
        }

        // Handle Nullable<T>
        if (typeName == "Nullable" && typeInfo.GenericArguments.Count == 1)
        {
            return MapToTypeScript(typeInfo.GenericArguments[0]);
        }

        // Handle Result<T>
        if (typeName == "Result" && typeInfo.GenericArguments.Count == 1)
        {
            var innerType = MapToTypeScript(typeInfo.GenericArguments[0]);
            return innerType == "void" ? "Result<void>" : $"Result<{innerType}>";
        }

        // Handle Task<T> - unwrap the Task
        if (typeName == "Task" && typeInfo.GenericArguments.Count == 1)
        {
            return MapToTypeScript(typeInfo.GenericArguments[0]);
        }

        // Handle ActionResult<T> - unwrap the ActionResult
        if (typeName == "ActionResult" && typeInfo.GenericArguments.Count == 1)
        {
            return MapToTypeScript(typeInfo.GenericArguments[0]);
        }

        // Handle Dictionary<TKey, TValue>
        if (IsDictionaryType(typeName) && typeInfo.GenericArguments.Count == 2)
        {
            var keyType = MapToTypeScript(typeInfo.GenericArguments[0]);
            var valueType = MapToTypeScript(typeInfo.GenericArguments[1]);

            // If key is string, use Record, otherwise use Map
            return keyType == "string"
                ? $"Record<string, {valueType}>"
                : $"Map<{keyType}, {valueType}>";
        }

        // Handle HashSet<T>
        if (IsSetType(typeName) && typeInfo.GenericArguments.Count == 1)
        {
            var elementType = MapToTypeScript(typeInfo.GenericArguments[0]);
            return $"Set<{elementType}>";
        }

        // For other generic types, preserve the generic structure
        if (typeInfo.GenericArguments.Count > 0)
        {
            var genericArgs = string.Join(", ", typeInfo.GenericArguments.Select(MapToTypeScript));
            return $"{typeName}<{genericArgs}>";
        }

        return typeName;
    }

    private string MapGenericTypeString(string csharpTypeName)
    {
        var genericStart = csharpTypeName.IndexOf('<');
        var genericEnd = csharpTypeName.LastIndexOf('>');

        if (genericStart == -1 || genericEnd == -1)
            return csharpTypeName;

        var baseType = csharpTypeName.Substring(0, genericStart).Trim();
        var genericArgsString = csharpTypeName.Substring(genericStart + 1, genericEnd - genericStart - 1);
        var genericArgs = SplitGenericArguments(genericArgsString);

        // Handle collections
        if (IsCollectionType(baseType))
        {
            if (genericArgs.Count > 0)
            {
                var elementType = MapToTypeScript(genericArgs[0]);
                return $"{elementType}[]";
            }
            return "any[]";
        }

        // Handle Nullable<T>
        if (baseType is "Nullable" or "System.Nullable")
        {
            return genericArgs.Count > 0 ? MapToTypeScript(genericArgs[0]) : "any";
        }

        // Handle Result<T>
        if (baseType == "Result")
        {
            if (genericArgs.Count > 0)
            {
                var innerType = MapToTypeScript(genericArgs[0]);
                return innerType == "void" ? "Result<void>" : $"Result<{innerType}>";
            }
            return "Result<void>";
        }

        // Handle Task<T> - unwrap
        if (baseType == "Task")
        {
            return genericArgs.Count > 0 ? MapToTypeScript(genericArgs[0]) : "void";
        }

        // Handle ActionResult<T> - unwrap
        if (baseType == "ActionResult" || baseType == "IActionResult")
        {
            return genericArgs.Count > 0 ? MapToTypeScript(genericArgs[0]) : "any";
        }

        // Handle Dictionary<TKey, TValue>
        if (IsDictionaryType(baseType) && genericArgs.Count == 2)
        {
            var keyType = MapToTypeScript(genericArgs[0]);
            var valueType = MapToTypeScript(genericArgs[1]);

            return keyType == "string"
                ? $"Record<string, {valueType}>"
                : $"Map<{keyType}, {valueType}>";
        }

        // Handle HashSet<T>
        if (IsSetType(baseType) && genericArgs.Count == 1)
        {
            var elementType = MapToTypeScript(genericArgs[0]);
            return $"Set<{elementType}>";
        }

        // For other generic types, preserve structure
        if (genericArgs.Count > 0)
        {
            var mappedArgs = string.Join(", ", genericArgs.Select(MapToTypeScript));
            return $"{baseType}<{mappedArgs}>";
        }

        return baseType;
    }

    private static List<string> SplitGenericArguments(string genericArgsString)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in genericArgsString)
        {
            if (ch == '<')
            {
                depth++;
                current.Append(ch);
            }
            else if (ch == '>')
            {
                depth--;
                current.Append(ch);
            }
            else if (ch == ',' && depth == 0)
            {
                args.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString().Trim());
        }

        return args;
    }

    private static bool IsCollectionType(string typeName)
    {
        return typeName is "List" or "IList" or "IEnumerable" or "ICollection" or
               "IReadOnlyList" or "IReadOnlyCollection" or "Array" or
               "System.Collections.Generic.List" or "System.Collections.Generic.IList" or
               "System.Collections.Generic.IEnumerable" or "System.Collections.Generic.ICollection" or
               "System.Collections.Generic.IReadOnlyList" or "System.Collections.Generic.IReadOnlyCollection";
    }

    private static bool IsDictionaryType(string typeName)
    {
        return typeName is "Dictionary" or "IDictionary" or "IReadOnlyDictionary" or
               "System.Collections.Generic.Dictionary" or "System.Collections.Generic.IDictionary" or
               "System.Collections.Generic.IReadOnlyDictionary";
    }

    private static bool IsSetType(string typeName)
    {
        return typeName is "HashSet" or "ISet" or
               "System.Collections.Generic.HashSet" or "System.Collections.Generic.ISet";
    }

    private static string ApplyNullability(string typeScriptType, bool isNullable)
    {
        if (!isNullable)
            return typeScriptType;

        // Don't add null to types that already handle it
        if (typeScriptType.Contains('|') || typeScriptType == "any" || typeScriptType == "void")
            return typeScriptType;

        return $"{typeScriptType} | null";
    }

    #endregion
}
