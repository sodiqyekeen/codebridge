namespace CodeBridge.Core.Attributes;

/// <summary>
/// Marks an endpoint method for inclusion in the generated TypeScript SDK.
/// Only endpoints with this attribute will be included in the SDK generation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class GenerateSdkAttribute : Attribute
{
    /// <summary>
    /// A brief summary of what this endpoint does. Used in generated documentation.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// The group/category this endpoint belongs to. Used to organize generated API functions.
    /// If not specified, endpoints will be grouped by their containing class name.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Whether this endpoint requires authentication. Defaults to true.
    /// </summary>
    public bool RequiresAuth { get; set; } = true;

    /// <summary>
    /// Additional tags for categorizing the endpoint.
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Indicates whether this endpoint is a file download endpoint.
    /// When true, the SDK will handle the response as a file download.
    /// </summary>
    public bool IsFileDownload { get; set; } = false;

    /// <summary>
    /// Indicates whether this endpoint is a file upload endpoint.
    /// When true, the SDK will handle the request as a file upload.
    /// </summary>
    public bool IsFileUpload { get; set; } = false;
}
