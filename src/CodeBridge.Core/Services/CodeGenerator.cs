using System.Text;
using CodeBridge.Core.Models;
using CodeBridge.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBridge.Core.Services;

/// <summary>
/// Generates TypeScript code from analyzed C# types and endpoints.
/// </summary>
public sealed class CodeGenerator : ICodeGenerator
{
    private readonly ILogger<CodeGenerator> _logger;
    private readonly ITypeMapper _typeMapper;
    private readonly TargetFramework _targetFramework;
    private readonly bool _includeValidation;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGenerator"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    /// <param name="typeMapper">Type mapper for C# to TypeScript conversions.</param>
    /// <param name="targetOptions">Target framework and language options.</param>
    /// <param name="featureOptions">Optional feature configuration.</param>
    public CodeGenerator(
        ILogger<CodeGenerator> logger,
        ITypeMapper typeMapper,
        TargetOptions targetOptions,
        FeatureOptions? featureOptions = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _targetFramework = targetOptions?.Framework ?? TargetFramework.React;
        _includeValidation = featureOptions?.IncludeValidation ?? true;
    }

    #region TypeScript Interface Generation

    /// <summary>
    /// Generates a TypeScript interface from a C# type.
    /// </summary>
    /// <param name="typeInfo">The type information to generate interface from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>TypeScript interface code.</returns>
    /// <exception cref="InvalidOperationException">Thrown when typeInfo is an enum type.</exception>
    public Task<string> GenerateTypeScriptInterfaceAsync(
        TypeInfo typeInfo,
        CancellationToken cancellationToken = default)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        if (typeInfo.IsEnum)
        {
            throw new InvalidOperationException("Use GenerateTypeScriptEnumAsync for enum types");
        }

        var sb = new StringBuilder();

        // Add JSDoc comment
        sb.AppendLine("/**");
        sb.AppendLine($" * {typeInfo.Name}");
        if (!string.IsNullOrEmpty(typeInfo.Namespace))
        {
            sb.AppendLine($" * @namespace {typeInfo.Namespace}");
        }
        sb.AppendLine(" */");

        // Generate interface declaration
        var genericParams = typeInfo.IsGeneric && typeInfo.GenericArguments.Count > 0
            ? $"<{string.Join(", ", typeInfo.GenericArguments.Select(g => g.Name))}>"
            : "";

        sb.AppendLine($"export interface {typeInfo.Name}{genericParams} {{");

        // Generate properties
        foreach (var property in typeInfo.Properties)
        {
            var propertyType = _typeMapper.MapToTypeScript(property.Type);
            var optional = !property.IsRequired ? "?" : "";

            // Add property comment if available
            if (!string.IsNullOrEmpty(property.Summary))
            {
                sb.AppendLine($"  /** {property.Summary} */");
            }

            sb.AppendLine($"  {ToCamelCase(property.Name)}{optional}: {propertyType};");
        }

        sb.AppendLine("}");

        return Task.FromResult(sb.ToString());
    }

    #endregion

    #region TypeScript Enum Generation

    /// <summary>
    /// Generates a TypeScript enum from a C# enum type.
    /// </summary>
    /// <param name="typeInfo">The enum type information to generate from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>TypeScript enum code.</returns>
    /// <exception cref="InvalidOperationException">Thrown when typeInfo is not an enum type.</exception>
    public Task<string> GenerateTypeScriptEnumAsync(
        TypeInfo typeInfo,
        CancellationToken cancellationToken = default)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        if (!typeInfo.IsEnum)
        {
            throw new InvalidOperationException("Use GenerateTypeScriptInterfaceAsync for non-enum types");
        }

        var sb = new StringBuilder();

        // Add JSDoc comment
        sb.AppendLine("/**");
        sb.AppendLine($" * {typeInfo.Name} enum");
        if (!string.IsNullOrEmpty(typeInfo.Namespace))
        {
            sb.AppendLine($" * @namespace {typeInfo.Namespace}");
        }
        sb.AppendLine(" */");

        // Generate enum declaration
        sb.AppendLine($"export enum {typeInfo.Name} {{");

        // Generate enum members
        for (var i = 0; i < typeInfo.EnumValues.Count; i++)
        {
            var enumValue = typeInfo.EnumValues[i];
            var comma = i < typeInfo.EnumValues.Count - 1 ? "," : "";
            sb.AppendLine($"  {enumValue.Name} = {enumValue.Value}{comma}");
        }

        sb.AppendLine("}");

        return Task.FromResult(sb.ToString());
    }

    #endregion

    #region API Client Function Generation

    /// <summary>
    /// Generates a TypeScript API client function for an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint information.</param>
    /// <param name="includeValidation">Whether to include validation calls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>TypeScript API function code.</returns>
    public Task<string> GenerateApiClientFunctionAsync(
        EndpointInfo endpoint,
        bool includeValidation = true,
        CancellationToken cancellationToken = default)
    {
        if (endpoint == null)
            throw new ArgumentNullException(nameof(endpoint));

        var sb = new StringBuilder();

        // Generate JSDoc comment
        sb.AppendLine("/**");
        if (!string.IsNullOrEmpty(endpoint.Summary))
        {
            sb.AppendLine($" * {endpoint.Summary}");
        }
        if (endpoint.RequiresAuthentication)
        {
            sb.AppendLine(" * @requires authentication");
        }
        if (endpoint.IsFileDownload)
        {
            sb.AppendLine(" * @returns File download");
        }
        if (endpoint.IsFileUpload)
        {
            sb.AppendLine(" * @param file - File to upload");
        }
        sb.AppendLine(" */");

        // Generate function signature
        var functionName = GenerateFunctionName(endpoint);
        var parameters = GenerateFunctionParameters(endpoint);
        var returnType = GenerateReturnType(endpoint);

        sb.AppendLine($"export async function {functionName}({parameters}): Promise<{returnType}> {{");

        // Generate function body
        if (includeValidation && _includeValidation && endpoint.RequestType != null)
        {
            sb.AppendLine($"  // Validate request");
            sb.AppendLine($"  validate{endpoint.RequestType.Name}Schema(data);");
            sb.AppendLine();
        }

        // Generate HTTP request
        var httpMethod = endpoint.HttpMethod.ToLowerInvariant();
        var route = GenerateRouteString(endpoint);

        if (endpoint.IsFileUpload)
        {
            sb.AppendLine($"  const formData = new FormData();");
            sb.AppendLine($"  formData.append('file', file);");
            sb.AppendLine();
            sb.AppendLine($"  return http.{httpMethod}<{returnType}>({route}, formData);");
        }
        else if (endpoint.IsFileDownload)
        {
            sb.AppendLine($"  return http.downloadFile({route});");
        }
        else if (httpMethod == "get" || httpMethod == "delete")
        {
            if (endpoint.RequestType != null)
            {
                sb.AppendLine($"  return http.{httpMethod}<{returnType}>({route}, {{ params: data }});");
            }
            else
            {
                sb.AppendLine($"  return http.{httpMethod}<{returnType}>({route});");
            }
        }
        else // POST, PUT, PATCH
        {
            if (endpoint.RequestType != null)
            {
                sb.AppendLine($"  return http.{httpMethod}<{returnType}>({route}, data);");
            }
            else
            {
                sb.AppendLine($"  return http.{httpMethod}<{returnType}>({route});");
            }
        }

        sb.AppendLine("}");

        return Task.FromResult(sb.ToString());
    }

    #endregion

    #region React Hook Generation

    /// <summary>
    /// Generates a React Query hook (useQuery or useMutation) for an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>React hook code using React Query.</returns>
    public Task<string> GenerateReactHookAsync(
        EndpointInfo endpoint,
        CancellationToken cancellationToken = default)
    {
        if (endpoint == null)
            throw new ArgumentNullException(nameof(endpoint));

        var sb = new StringBuilder();
        var httpMethod = endpoint.HttpMethod.ToUpperInvariant();
        var functionName = GenerateFunctionName(endpoint);
        var hookName = $"use{ToPascalCase(functionName)}";

        // Determine if this is a query or mutation
        var isQuery = httpMethod == "GET";

        sb.AppendLine("/**");
        sb.AppendLine($" * React hook for {endpoint.Summary ?? endpoint.ActionName}");
        if (endpoint.RequiresAuthentication)
        {
            sb.AppendLine(" * @requires authentication");
        }
        sb.AppendLine(" */");

        if (isQuery)
        {
            // Generate useQuery hook
            var returnType = GenerateReturnType(endpoint);
            var requestType = endpoint.RequestType != null ? _typeMapper.MapToTypeScript(endpoint.RequestType) : "void";

            if (endpoint.RequestType != null)
            {
                sb.AppendLine($"export function {hookName}(params: {requestType}) {{");
                sb.AppendLine($"  return useQuery({{");
                sb.AppendLine($"    queryKey: ['{endpoint.ActionName}', params],");
                sb.AppendLine($"    queryFn: () => {functionName}(params)");
                sb.AppendLine($"  }});");
            }
            else
            {
                sb.AppendLine($"export function {hookName}() {{");
                sb.AppendLine($"  return useQuery({{");
                sb.AppendLine($"    queryKey: ['{endpoint.ActionName}'],");
                sb.AppendLine($"    queryFn: () => {functionName}()");
                sb.AppendLine($"  }});");
            }
        }
        else
        {
            // Generate useMutation hook
            var requestType = endpoint.RequestType != null ? _typeMapper.MapToTypeScript(endpoint.RequestType) : "void";

            sb.AppendLine($"export function {hookName}() {{");
            sb.AppendLine($"  return useMutation({{");
            sb.AppendLine($"    mutationFn: (data: {requestType}) => {functionName}(data)");
            sb.AppendLine($"  }});");
        }

        sb.AppendLine("}");

        return Task.FromResult(sb.ToString());
    }

    #endregion

    #region Next.js Server Function Generation

    /// <summary>
    /// Generates a Next.js server-side function with 'use server' directive.
    /// </summary>
    /// <param name="endpoint">The endpoint information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Next.js server function code.</returns>
    public Task<string> GenerateNextJsServerFunctionAsync(
        EndpointInfo endpoint,
        CancellationToken cancellationToken = default)
    {
        if (endpoint == null)
            throw new ArgumentNullException(nameof(endpoint));

        var sb = new StringBuilder();
        var functionName = GenerateFunctionName(endpoint);
        var serverFunctionName = $"{functionName}Server";

        sb.AppendLine("/**");
        sb.AppendLine($" * Server-side function for {endpoint.Summary ?? endpoint.ActionName}");
        sb.AppendLine(" * @description Use this in Server Components or Server Actions");
        sb.AppendLine(" */");

        // Generate function signature
        var parameters = GenerateFunctionParameters(endpoint, includeFile: false);
        var returnType = GenerateReturnType(endpoint);

        sb.AppendLine($"export async function {serverFunctionName}({parameters}): Promise<{returnType}> {{");
        sb.AppendLine($"  'use server';");
        sb.AppendLine();

        // Generate server-side HTTP request (no auth token needed in server context)
        var httpMethod = endpoint.HttpMethod.ToLowerInvariant();
        var route = GenerateRouteString(endpoint);

        if (httpMethod == "get" || httpMethod == "delete")
        {
            if (endpoint.RequestType != null)
            {
                sb.AppendLine($"  return httpServer.{httpMethod}<{returnType}>({route}, {{ params: data }});");
            }
            else
            {
                sb.AppendLine($"  return httpServer.{httpMethod}<{returnType}>({route});");
            }
        }
        else // POST, PUT, PATCH
        {
            if (endpoint.RequestType != null)
            {
                sb.AppendLine($"  return httpServer.{httpMethod}<{returnType}>({route}, data);");
            }
            else
            {
                sb.AppendLine($"  return httpServer.{httpMethod}<{returnType}>({route});");
            }
        }

        sb.AppendLine("}");

        return Task.FromResult(sb.ToString());
    }

    #endregion

    #region Validation Schema Generation

    /// <summary>
    /// Generates a Zod validation schema from FluentValidation rules.
    /// </summary>
    /// <param name="typeInfo">The type to generate schema for.</param>
    /// <param name="validationRules">Validation rules discovered from FluentValidation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Zod validation schema code, or empty string if no rules found.</returns>
    public Task<string> GenerateValidationSchemaAsync(
        TypeInfo typeInfo,
        Dictionary<string, List<PropertyValidationRules>> validationRules,
        CancellationToken cancellationToken = default)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));

        if (!validationRules.TryGetValue(typeInfo.Name, out var rules) || rules.Count == 0)
        {
            return Task.FromResult(string.Empty);
        }

        var sb = new StringBuilder();

        sb.AppendLine("import { z } from 'zod';");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine($" * Validation schema for {typeInfo.Name}");
        sb.AppendLine(" */");
        sb.AppendLine($"export const {ToCamelCase(typeInfo.Name)}Schema = z.object({{");

        // Create a dictionary for quick lookup
        var rulesByProperty = rules.ToDictionary(r => r.PropertyName, r => r);

        foreach (var property in typeInfo.Properties)
        {
            if (!rulesByProperty.TryGetValue(property.Name, out var rule))
            {
                // No validation rules for this property, use basic type
                var basicType = GetZodType(property.Type);
                var optionalSuffix = !property.IsRequired ? ".optional()" : "";
                sb.AppendLine($"  {ToCamelCase(property.Name)}: {basicType}{optionalSuffix},");
                continue;
            }

            // Build Zod validation chain
            var zodType = GetZodType(property.Type);
            var validations = new List<string>();

            if (rule.Required == true || property.IsRequired)
            {
                validations.Add($".min(1, 'Required')");
            }

            if (rule.MinLength.HasValue)
            {
                validations.Add($".min({rule.MinLength.Value}, 'Minimum length is {rule.MinLength.Value}')");
            }

            if (rule.MaxLength.HasValue)
            {
                validations.Add($".max({rule.MaxLength.Value}, 'Maximum length is {rule.MaxLength.Value}')");
            }

            if (rule.Email == true)
            {
                validations.Add($".email('Invalid email address')");
            }

            if (!string.IsNullOrEmpty(rule.Pattern))
            {
                var pattern = rule.Pattern.Replace("\\", "\\\\").Replace("'", "\\'");
                validations.Add($".regex(/{pattern}/, 'Invalid format')");
            }

            var validationChain = string.Join("", validations);
            var optional = !property.IsRequired && rule.Required != true ? ".optional()" : "";

            sb.AppendLine($"  {ToCamelCase(property.Name)}: {zodType}{validationChain}{optional},");
        }

        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine($"export type {typeInfo.Name} = z.infer<typeof {ToCamelCase(typeInfo.Name)}Schema>;");

        return Task.FromResult(sb.ToString());
    }

    #endregion

    #region Barrel Export Generation

    /// <summary>
    /// Generates a barrel export file (index.ts) that re-exports all modules.
    /// </summary>
    /// <param name="exports">List of module names to export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Barrel export code, or empty string if no exports.</returns>
    public Task<string> GenerateBarrelExportAsync(
        List<string> exports,
        CancellationToken cancellationToken = default)
    {
        if (exports == null || exports.Count == 0)
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();

        foreach (var export in exports.OrderBy(e => e))
        {
            sb.AppendLine($"export * from './{export}';");
        }

        return Task.FromResult(sb.ToString());
    }

    #endregion

    #region Helper Methods

    private string GenerateFunctionName(EndpointInfo endpoint)
    {
        // Convert HTTP method and action to camelCase function name
        // GET /products -> getProducts
        // POST /products -> createProduct
        // PUT /products/{id} -> updateProduct
        // DELETE /products/{id} -> deleteProduct

        var action = endpoint.ActionName;
        var method = endpoint.HttpMethod.ToLowerInvariant();

        // If action already contains the HTTP method, just use it
        if (action.ToLowerInvariant().StartsWith(method))
        {
            return ToCamelCase(action);
        }

        // Otherwise, combine method with action
        return ToCamelCase($"{method}{action}");
    }

    private string GenerateFunctionParameters(EndpointInfo endpoint, bool includeFile = true)
    {
        var parameters = new List<string>();

        // Add request body parameter
        if (endpoint.RequestType != null)
        {
            var paramType = _typeMapper.MapToTypeScript(endpoint.RequestType);
            parameters.Add($"data: {paramType}");
        }

        // Add file parameter for file uploads
        if (includeFile && endpoint.IsFileUpload)
        {
            parameters.Add("file: File");
        }

        // Add route parameters
        foreach (var param in endpoint.Parameters.Where(p => p.Source == ParameterSource.Route))
        {
            var paramType = _typeMapper.MapToTypeScript(param.Type);
            parameters.Add($"{ToCamelCase(param.Name)}: {paramType}");
        }

        return string.Join(", ", parameters);
    }

    private string GenerateReturnType(EndpointInfo endpoint)
    {
        if (endpoint.IsFileDownload)
        {
            return "Blob";
        }

        if (endpoint.ResponseType != null)
        {
            return _typeMapper.MapToTypeScript(endpoint.ResponseType);
        }

        return "void";
    }

    private string GenerateRouteString(EndpointInfo endpoint)
    {
        var route = endpoint.Route;

        // Replace route parameters with template literals
        // /products/{id} -> `/products/${id}`
        foreach (var param in endpoint.Parameters.Where(p => p.Source == ParameterSource.Route))
        {
            route = route.Replace($"{{{param.Name}}}", $"${{{ToCamelCase(param.Name)}}}");
        }

        return $"`{route}`";
    }

    private string GetZodType(TypeInfo typeInfo)
    {
        var tsType = _typeMapper.MapToTypeScript(typeInfo);

        return tsType switch
        {
            "string" => "z.string()",
            "number" => "z.number()",
            "boolean" => "z.boolean()",
            "Date" => "z.date()",
            _ when tsType.EndsWith("[]") => $"z.array({GetZodTypeForString(tsType.TrimEnd('[', ']'))})",
            _ => "z.any()"
        };
    }

    private static string GetZodTypeForString(string tsType)
    {
        return tsType switch
        {
            "string" => "z.string()",
            "number" => "z.number()",
            "boolean" => "z.boolean()",
            "Date" => "z.date()",
            _ => "z.any()"
        };
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsUpper(value[0]))
            return value;

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    #endregion
}
