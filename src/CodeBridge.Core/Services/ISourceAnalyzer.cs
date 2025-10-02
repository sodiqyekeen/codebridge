using CodeBridge.Core.Models;

namespace CodeBridge.Core.Services;

/// <summary>
/// Analyzes .NET source code to discover API endpoints and types.
/// </summary>
public interface ISourceAnalyzer
{
    /// <summary>
    /// Discovers all API endpoints from the specified projects.
    /// Supports both Controller-based and Minimal API endpoints.
    /// </summary>
    Task<List<EndpointInfo>> DiscoverEndpointsAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes and extracts type information from the specified projects.
    /// Includes DTOs, request/response models, enums, and other API types.
    /// </summary>
    Task<List<TypeInfo>> DiscoverTypesAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes validation rules from FluentValidation validators.
    /// </summary>
    Task<Dictionary<string, List<PropertyValidationRules>>> DiscoverValidationRulesAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts detailed parameter information for an endpoint.
    /// Includes route parameters, query parameters, and request body.
    /// </summary>
    Task<List<ParameterInfo>> ExtractParametersAsync(
        EndpointInfo endpoint,
        List<TypeInfo> discoveredTypes,
        CancellationToken cancellationToken = default);
}
