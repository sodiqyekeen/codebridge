namespace CodeBridge.Core.Models;

/// <summary>
/// Information about a property in a type.
/// </summary>
public class PropertyInfo
{
    /// <summary>
    /// Property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Property type.
    /// </summary>
    public required TypeInfo Type { get; init; }

    /// <summary>
    /// Whether the property is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Whether the property is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Default value (if any).
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// XML documentation summary.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Validation rules for this property.
    /// </summary>
    public PropertyValidationRules? ValidationRules { get; init; }
}
