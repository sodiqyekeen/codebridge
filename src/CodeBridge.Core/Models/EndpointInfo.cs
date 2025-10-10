namespace CodeBridge.Core.Models;

/// <summary>
/// Information about a discovered API endpoint.
/// </summary>
public class EndpointInfo
{
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// Route template (e.g., "/api/products/{id}").
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Controller or handler name.
    /// </summary>
    public required string ControllerName { get; init; }

    /// <summary>
    /// Action method name.
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// Request type (if any).
    /// </summary>
    public TypeInfo? RequestType { get; init; }

    /// <summary>
    /// Response type.
    /// </summary>
    public TypeInfo? ResponseType { get; init; }

    /// <summary>
    /// Route parameters.
    /// </summary>
    public List<ParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// Whether authentication is required.
    /// </summary>
    public bool RequiresAuthentication { get; init; }

    /// <summary>
    /// Required authorization policies.
    /// </summary>
    public List<string> AuthorizationPolicies { get; init; } = new();

    /// <summary>
    /// XML documentation summary.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// The group/category this endpoint belongs to. Used to organize generated API functions.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Additional tags for categorizing the endpoint.
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Indicates whether this endpoint is a file download endpoint.
    /// </summary>
    public bool IsFileDownload { get; init; }

    /// <summary>
    /// Indicates whether this endpoint is a file upload endpoint.
    /// </summary>
    public bool IsFileUpload { get; init; }

    /// <summary>
    /// Name of the type marked with [AsParameters] attribute (for query parameter expansion).
    /// </summary>
    public string? AsParametersType { get; init; }

    /// <summary>
    /// Generated function name for SDK.
    /// </summary>
    public string FunctionName => GenerateFunctionName();

    private string GenerateFunctionName()
    {
        // Convert to camelCase: GetProducts -> getProducts
        var name = $"{HttpMethod.ToLowerInvariant()}{ActionName}";
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
