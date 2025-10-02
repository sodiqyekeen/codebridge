namespace CodeBridge.Core.Models;

/// <summary>
/// Validation rules for a type or property.
/// </summary>
public class ValidationRules
{
    /// <summary>
    /// Custom validation rules.
    /// </summary>
    public List<string> CustomRules { get; init; } = new();
}

/// <summary>
/// Validation rules for a property.
/// </summary>
public class PropertyValidationRules
{
    /// <summary>
    /// Property name.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Whether the property is required.
    /// </summary>
    public bool? Required { get; init; }

    /// <summary>
    /// Minimum length (for strings).
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    /// Maximum length (for strings).
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Minimum value (for numbers).
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Maximum value (for numbers).
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Regex pattern.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Email validation.
    /// </summary>
    public bool? Email { get; init; }

    /// <summary>
    /// URL validation.
    /// </summary>
    public bool? Url { get; init; }

    /// <summary>
    /// Custom error message.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
