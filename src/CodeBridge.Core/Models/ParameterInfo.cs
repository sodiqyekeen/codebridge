namespace CodeBridge.Core.Models;

/// <summary>
/// Information about an endpoint parameter.
/// </summary>
public class ParameterInfo
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parameter type.
    /// </summary>
    public required TypeInfo Type { get; init; }

    /// <summary>
    /// Parameter binding source (FromRoute, FromQuery, FromBody, etc.).
    /// </summary>
    public ParameterSource Source { get; init; }

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Default value (if any).
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// XML documentation summary.
    /// </summary>
    public string? Summary { get; init; }
}

/// <summary>
/// Parameter binding source.
/// </summary>
public enum ParameterSource
{
    /// <summary>
    /// From URL route.
    /// </summary>
    Route,

    /// <summary>
    /// From query string.
    /// </summary>
    Query,

    /// <summary>
    /// From request body.
    /// </summary>
    Body,

    /// <summary>
    /// From HTTP header.
    /// </summary>
    Header,

    /// <summary>
    /// From form data.
    /// </summary>
    Form
}
