namespace CodeBridge.Core.Models;

/// <summary>
/// Represents a FluentValidation rule extracted from a validator class.
/// Used to generate client-side TypeScript validation schemas.
/// </summary>
public sealed class ValidationRule
{
    /// <summary>
    /// Gets or sets the name of the type being validated (e.g., CreateUserCommand).
    /// </summary>
    public required string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the property name this rule applies to.
    /// </summary>
    public required string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the type of validation rule.
    /// Common values: required, email, minLength, maxLength, pattern, min, max, matches, greaterThan, lessThan.
    /// </summary>
    public required string RuleType { get; set; }

    /// <summary>
    /// Gets or sets the value associated with the rule (e.g., min length value, regex pattern).
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the custom error message for this validation rule.
    /// If null, a default message will be generated.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for complex validation rules.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
