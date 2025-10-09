using CodeBridge.Core.Models;

namespace CodeBridge.Core.Services;

/// <summary>
/// Generates TypeScript code from analyzed C# types and endpoints.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates TypeScript interface from a type.
    /// </summary>
    Task<string> GenerateTypeScriptInterfaceAsync(TypeInfo typeInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates TypeScript enum from a type.
    /// </summary>
    Task<string> GenerateTypeScriptEnumAsync(TypeInfo typeInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates API client function for an endpoint.
    /// </summary>
    Task<string> GenerateApiClientFunctionAsync(
        EndpointInfo endpoint,
        bool includeValidation = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates React hook for an endpoint.
    /// </summary>
    Task<string> GenerateReactHookAsync(
        EndpointInfo endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates Next.js server function for an endpoint.
    /// </summary>
    Task<string> GenerateNextJsServerFunctionAsync(
        EndpointInfo endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates validation schema (Zod) for a type.
    /// </summary>
    Task<string> GenerateValidationSchemaAsync(
        TypeInfo typeInfo,
        Dictionary<string, List<PropertyValidationRules>> validationRules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates barrel export file (index.ts).
    /// </summary>
    Task<string> GenerateBarrelExportAsync(
        List<string> exports,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates HTTP service implementation.
    /// </summary>
    Task<string> GenerateHttpServiceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates API client file with imports for a group of endpoints.
    /// </summary>
    Task<string> GenerateApiClientFileAsync(
        string groupName,
        List<EndpointInfo> endpoints,
        bool includeValidation = true,
        CancellationToken cancellationToken = default);
}
