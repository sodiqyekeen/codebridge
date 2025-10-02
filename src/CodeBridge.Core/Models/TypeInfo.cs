namespace CodeBridge.Core.Models;

/// <summary>
/// Information about a C# type to be mapped to TypeScript.
/// </summary>
public class TypeInfo
{
    /// <summary>
    /// Full type name including namespace.
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Short type name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type namespace.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Whether this is a generic type.
    /// </summary>
    public bool IsGeneric { get; init; }

    /// <summary>
    /// Generic type arguments.
    /// </summary>
    public List<TypeInfo> GenericArguments { get; init; } = new();

    /// <summary>
    /// Whether this is a collection type.
    /// </summary>
    public bool IsCollection { get; init; }

    /// <summary>
    /// Whether this is a nullable type.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Whether this is an enum type.
    /// </summary>
    public bool IsEnum { get; init; }

    /// <summary>
    /// Enum values (if IsEnum is true).
    /// </summary>
    public List<EnumValue> EnumValues { get; init; } = new();

    /// <summary>
    /// Type properties.
    /// </summary>
    public List<PropertyInfo> Properties { get; init; } = new();

    /// <summary>
    /// XML documentation summary.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Validation rules for this type.
    /// </summary>
    public ValidationRules? ValidationRules { get; init; }
}

/// <summary>
/// Information about an enum value.
/// </summary>
public class EnumValue
{
    /// <summary>
    /// Enum member name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Enum member value.
    /// </summary>
    public required int Value { get; init; }

    /// <summary>
    /// XML documentation summary.
    /// </summary>
    public string? Summary { get; init; }
}
