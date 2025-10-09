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

        // Generate function body - validate only if this endpoint has validation rules
        if (includeValidation && endpoint.RequestType != null)
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

    #region HTTP Service and API Client File Generation

    /// <summary>
    /// Generates the HTTP service implementation with axios, storage interface, and token refresh.
    /// </summary>
    public Task<string> GenerateHttpServiceAsync(CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        // Storage interface and implementations
        sb.AppendLine("import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse, AxiosError } from 'axios';");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * Storage interface for token persistence");
        sb.AppendLine(" * Allows choosing between localStorage, sessionStorage, or custom implementations");
        sb.AppendLine(" */");
        sb.AppendLine("export interface IStorage {");
        sb.AppendLine("  getItem(key: string): string | null;");
        sb.AppendLine("  setItem(key: string, value: string): void;");
        sb.AppendLine("  removeItem(key: string): void;");
        sb.AppendLine("  clear(): void;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * LocalStorage implementation (persists across browser sessions)");
        sb.AppendLine(" */");
        sb.AppendLine("export class LocalStorageAdapter implements IStorage {");
        sb.AppendLine("  getItem(key: string): string | null {");
        sb.AppendLine("    if (typeof window === 'undefined') return null;");
        sb.AppendLine("    return localStorage.getItem(key);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  setItem(key: string, value: string): void {");
        sb.AppendLine("    if (typeof window === 'undefined') return;");
        sb.AppendLine("    localStorage.setItem(key, value);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  removeItem(key: string): void {");
        sb.AppendLine("    if (typeof window === 'undefined') return;");
        sb.AppendLine("    localStorage.removeItem(key);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  clear(): void {");
        sb.AppendLine("    if (typeof window === 'undefined') return;");
        sb.AppendLine("    localStorage.clear();");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * SessionStorage implementation (clears when browser tab closes)");
        sb.AppendLine(" */");
        sb.AppendLine("export class SessionStorageAdapter implements IStorage {");
        sb.AppendLine("  getItem(key: string): string | null {");
        sb.AppendLine("    if (typeof window === 'undefined') return null;");
        sb.AppendLine("    return sessionStorage.getItem(key);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  setItem(key: string, value: string): void {");
        sb.AppendLine("    if (typeof window === 'undefined') return;");
        sb.AppendLine("    sessionStorage.setItem(key, value);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  removeItem(key: string): void {");
        sb.AppendLine("    if (typeof window === 'undefined') return;");
        sb.AppendLine("    sessionStorage.removeItem(key);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  clear(): void {");
        sb.AppendLine("    if (typeof window === 'undefined') return;");
        sb.AppendLine("    sessionStorage.clear();");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * In-memory storage implementation (clears on page reload)");
        sb.AppendLine(" * Useful for server-side rendering or testing");
        sb.AppendLine(" */");
        sb.AppendLine("export class MemoryStorageAdapter implements IStorage {");
        sb.AppendLine("  private storage: Map<string, string> = new Map();");
        sb.AppendLine();
        sb.AppendLine("  getItem(key: string): string | null {");
        sb.AppendLine("    return this.storage.get(key) ?? null;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  setItem(key: string, value: string): void {");
        sb.AppendLine("    this.storage.set(key, value);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  removeItem(key: string): void {");
        sb.AppendLine("    this.storage.delete(key);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  clear(): void {");
        sb.AppendLine("    this.storage.clear();");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * HTTP service configuration options");
        sb.AppendLine(" */");
        sb.AppendLine("export interface HttpServiceConfig {");
        sb.AppendLine("  /** Base URL for API requests */");
        sb.AppendLine("  baseURL: string;");
        sb.AppendLine("  /** Storage adapter for token persistence (default: LocalStorage) */");
        sb.AppendLine("  storage?: IStorage;");
        sb.AppendLine("  /** Token storage key (default: 'token') */");
        sb.AppendLine("  tokenKey?: string;");
        sb.AppendLine("  /** Refresh token storage key (default: 'refreshToken') */");
        sb.AppendLine("  refreshTokenKey?: string;");
        sb.AppendLine("  /** Endpoint for token refresh (default: '/auth/refresh') */");
        sb.AppendLine("  refreshEndpoint?: string;");
        sb.AppendLine("  /** Enable automatic token refresh (default: true) */");
        sb.AppendLine("  enableTokenRefresh?: boolean;");
        sb.AppendLine("  /** Callback on authentication failure */");
        sb.AppendLine("  onAuthError?: () => void;");
        sb.AppendLine("  /** Callback on token refresh */");
        sb.AppendLine("  onTokenRefreshed?: (token: string) => void;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * Token refresh response interface");
        sb.AppendLine(" */");
        sb.AppendLine("interface TokenRefreshResponse {");
        sb.AppendLine("  token: string;");
        sb.AppendLine("  refreshToken?: string;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * HTTP service wrapper for API calls with automatic token refresh");
        sb.AppendLine(" */");
        sb.AppendLine("class HttpService {");
        sb.AppendLine("  private client: AxiosInstance;");
        sb.AppendLine("  private storage: IStorage;");
        sb.AppendLine("  private tokenKey: string;");
        sb.AppendLine("  private refreshTokenKey: string;");
        sb.AppendLine("  private refreshEndpoint: string;");
        sb.AppendLine("  private enableTokenRefresh: boolean;");
        sb.AppendLine("  private onAuthError?: () => void;");
        sb.AppendLine("  private onTokenRefreshed?: (token: string) => void;");
        sb.AppendLine("  private isRefreshing = false;");
        sb.AppendLine("  private refreshSubscribers: ((token: string) => void)[] = [];");
        sb.AppendLine();
        sb.AppendLine("  constructor(config: HttpServiceConfig) {");
        sb.AppendLine("    this.storage = config.storage ?? new LocalStorageAdapter();");
        sb.AppendLine("    this.tokenKey = config.tokenKey ?? 'token';");
        sb.AppendLine("    this.refreshTokenKey = config.refreshTokenKey ?? 'refreshToken';");
        sb.AppendLine("    this.refreshEndpoint = config.refreshEndpoint ?? '/auth/refresh';");
        sb.AppendLine("    this.enableTokenRefresh = config.enableTokenRefresh ?? true;");
        sb.AppendLine("    this.onAuthError = config.onAuthError;");
        sb.AppendLine("    this.onTokenRefreshed = config.onTokenRefreshed;");
        sb.AppendLine();
        sb.AppendLine("    this.client = axios.create({");
        sb.AppendLine("      baseURL: config.baseURL,");
        sb.AppendLine("      headers: {");
        sb.AppendLine("        'Content-Type': 'application/json',");
        sb.AppendLine("      },");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    this.setupInterceptors();");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /**");
        sb.AppendLine("   * Set up request and response interceptors");
        sb.AppendLine("   */");
        sb.AppendLine("  private setupInterceptors(): void {");
        sb.AppendLine("    // Request interceptor: Add auth token to requests");
        sb.AppendLine("    this.client.interceptors.request.use(");
        sb.AppendLine("      (config) => {");
        sb.AppendLine("        const token = this.storage.getItem(this.tokenKey);");
        sb.AppendLine("        if (token) {");
        sb.AppendLine("          config.headers.Authorization = `Bearer ${token}`;");
        sb.AppendLine("        }");
        sb.AppendLine("        return config;");
        sb.AppendLine("      },");
        sb.AppendLine("      (error) => Promise.reject(error)");
        sb.AppendLine("    );");
        sb.AppendLine();
        sb.AppendLine("    // Response interceptor: Handle errors and token refresh");
        sb.AppendLine("    this.client.interceptors.response.use(");
        sb.AppendLine("      (response) => response,");
        sb.AppendLine("      async (error: AxiosError) => {");
        sb.AppendLine("        const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };");
        sb.AppendLine();
        sb.AppendLine("        // Handle 401 Unauthorized");
        sb.AppendLine("        if (error.response?.status === 401 && !originalRequest._retry) {");
        sb.AppendLine("          if (this.enableTokenRefresh) {");
        sb.AppendLine("            originalRequest._retry = true;");
        sb.AppendLine();
        sb.AppendLine("            try {");
        sb.AppendLine("              const token = await this.refreshToken();");
        sb.AppendLine("              if (originalRequest.headers) {");
        sb.AppendLine("                originalRequest.headers.Authorization = `Bearer ${token}`;");
        sb.AppendLine("              }");
        sb.AppendLine("              return this.client(originalRequest);");
        sb.AppendLine("            } catch (refreshError) {");
        sb.AppendLine("              this.handleAuthError();");
        sb.AppendLine("              return Promise.reject(refreshError);");
        sb.AppendLine("            }");
        sb.AppendLine("          } else {");
        sb.AppendLine("            this.handleAuthError();");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return Promise.reject(error);");
        sb.AppendLine("      }");
        sb.AppendLine("    );");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /**");
        sb.AppendLine("   * Refresh the access token using refresh token");
        sb.AppendLine("   */");
        sb.AppendLine("  private async refreshToken(): Promise<string> {");
        sb.AppendLine("    if (this.isRefreshing) {");
        sb.AppendLine("      // If already refreshing, wait for it to complete");
        sb.AppendLine("      return new Promise((resolve) => {");
        sb.AppendLine("        this.refreshSubscribers.push((token: string) => resolve(token));");
        sb.AppendLine("      });");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    this.isRefreshing = true;");
        sb.AppendLine();
        sb.AppendLine("    try {");
        sb.AppendLine("      const refreshToken = this.storage.getItem(this.refreshTokenKey);");
        sb.AppendLine("      if (!refreshToken) {");
        sb.AppendLine("        throw new Error('No refresh token available');");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      const response = await axios.post<TokenRefreshResponse>(");
        sb.AppendLine("        `${this.client.defaults.baseURL}${this.refreshEndpoint}`,");
        sb.AppendLine("        { refreshToken }");
        sb.AppendLine("      );");
        sb.AppendLine();
        sb.AppendLine("      const { token, refreshToken: newRefreshToken } = response.data;");
        sb.AppendLine();
        sb.AppendLine("      // Store new tokens");
        sb.AppendLine("      this.storage.setItem(this.tokenKey, token);");
        sb.AppendLine("      if (newRefreshToken) {");
        sb.AppendLine("        this.storage.setItem(this.refreshTokenKey, newRefreshToken);");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      // Notify callback");
        sb.AppendLine("      if (this.onTokenRefreshed) {");
        sb.AppendLine("        this.onTokenRefreshed(token);");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      // Notify all waiting requests");
        sb.AppendLine("      this.refreshSubscribers.forEach((callback) => callback(token));");
        sb.AppendLine("      this.refreshSubscribers = [];");
        sb.AppendLine();
        sb.AppendLine("      return token;");
        sb.AppendLine("    } catch (error) {");
        sb.AppendLine("      this.refreshSubscribers = [];");
        sb.AppendLine("      throw error;");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      this.isRefreshing = false;");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /**");
        sb.AppendLine("   * Handle authentication errors");
        sb.AppendLine("   */");
        sb.AppendLine("  private handleAuthError(): void {");
        sb.AppendLine("    this.clearTokens();");
        sb.AppendLine("    if (this.onAuthError) {");
        sb.AppendLine("      this.onAuthError();");
        sb.AppendLine("    } else if (typeof window !== 'undefined') {");
        sb.AppendLine("      // Default behavior: redirect to login");
        sb.AppendLine("      window.location.href = '/login';");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /**");
        sb.AppendLine("   * Store authentication tokens");
        sb.AppendLine("   */");
        sb.AppendLine("  public setTokens(token: string, refreshToken?: string): void {");
        sb.AppendLine("    this.storage.setItem(this.tokenKey, token);");
        sb.AppendLine("    if (refreshToken) {");
        sb.AppendLine("      this.storage.setItem(this.refreshTokenKey, refreshToken);");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /**");
        sb.AppendLine("   * Get current access token");
        sb.AppendLine("   */");
        sb.AppendLine("  public getToken(): string | null {");
        sb.AppendLine("    return this.storage.getItem(this.tokenKey);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /**");
        sb.AppendLine("   * Clear all authentication tokens");
        sb.AppendLine("   */");
        sb.AppendLine("  public clearTokens(): void {");
        sb.AppendLine("    this.storage.removeItem(this.tokenKey);");
        sb.AppendLine("    this.storage.removeItem(this.refreshTokenKey);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /**");
        sb.AppendLine("   * Check if user is authenticated");
        sb.AppendLine("   */");
        sb.AppendLine("  public isAuthenticated(): boolean {");
        sb.AppendLine("    return !!this.getToken();");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  async get<T>(url: string, config?: AxiosRequestConfig): Promise<T> {");
        sb.AppendLine("    const response: AxiosResponse<T> = await this.client.get(url, config);");
        sb.AppendLine("    return response.data;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  async post<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {");
        sb.AppendLine("    const response: AxiosResponse<T> = await this.client.post(url, data, config);");
        sb.AppendLine("    return response.data;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  async put<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {");
        sb.AppendLine("    const response: AxiosResponse<T> = await this.client.put(url, data, config);");
        sb.AppendLine("    return response.data;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  async delete<T>(url: string, config?: AxiosRequestConfig): Promise<T> {");
        sb.AppendLine("    const response: AxiosResponse<T> = await this.client.delete(url, config);");
        sb.AppendLine("    return response.data;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  async patch<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {");
        sb.AppendLine("    const response: AxiosResponse<T> = await this.client.patch(url, data, config);");
        sb.AppendLine("    return response.data;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  async downloadFile(url: string, config?: AxiosRequestConfig): Promise<Blob> {");
        sb.AppendLine("    const response: AxiosResponse<Blob> = await this.client.get(url, {");
        sb.AppendLine("      ...config,");
        sb.AppendLine("      responseType: 'blob',");
        sb.AppendLine("    });");
        sb.AppendLine("    return response.data;");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// Create and export singleton instance with configurable storage");
        sb.AppendLine("// Default: LocalStorage (persists across sessions)");
        sb.AppendLine("// To use SessionStorage: http.storage = new SessionStorageAdapter()");
        sb.AppendLine("// To use MemoryStorage: http.storage = new MemoryStorageAdapter()");
        sb.AppendLine("const http = new HttpService({");
        sb.AppendLine("  baseURL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000',");
        sb.AppendLine("  storage: new LocalStorageAdapter(), // Change to SessionStorageAdapter() if needed");
        sb.AppendLine("  enableTokenRefresh: true,");
        sb.AppendLine("  refreshEndpoint: '/auth/refresh',");
        sb.AppendLine("  onAuthError: () => {");
        sb.AppendLine("    // Custom auth error handling");
        sb.AppendLine("    console.error('Authentication failed. Redirecting to login...');");
        sb.AppendLine("    if (typeof window !== 'undefined') {");
        sb.AppendLine("      window.location.href = '/login';");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  onTokenRefreshed: (_token) => {");
        sb.AppendLine("    console.log('Token refreshed successfully');");
        sb.AppendLine("  },");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("export default http;");
        sb.AppendLine("export { HttpService };");

        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Generates a complete API client file with imports for a group of endpoints.
    /// </summary>
    public async Task<string> GenerateApiClientFileAsync(
        string groupName,
        List<EndpointInfo> endpoints,
        bool includeValidation = true,
        Dictionary<string, List<PropertyValidationRules>>? validationRules = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        // Add imports
        sb.AppendLine("import http from '../lib/httpService';");

        // Collect all unique types used in this file
        var usedTypes = new HashSet<string>();
        var validationImports = new HashSet<string>();

        foreach (var endpoint in endpoints)
        {
            if (endpoint.RequestType != null)
            {
                ExtractTypeNames(endpoint.RequestType.Name, usedTypes);

                // Only add validation import if this type has validation rules
                if (includeValidation && validationRules != null &&
                    validationRules.ContainsKey(endpoint.RequestType.Name))
                {
                    validationImports.Add($"validate{endpoint.RequestType.Name}Schema");
                }
            }
            if (endpoint.ResponseType != null)
                ExtractTypeNames(endpoint.ResponseType.Name, usedTypes);

            // Extract types from return type (e.g., Result<void>, Result<User>, etc.)
            var returnType = GenerateReturnType(endpoint);
            if (!string.IsNullOrEmpty(returnType))
                ExtractTypeNames(returnType, usedTypes);
        }

        // Remove framework types that shouldn't be imported
        usedTypes.Remove("IResult");
        usedTypes.Remove("void");
        usedTypes.Remove("any");
        usedTypes.Remove("string");
        usedTypes.Remove("number");
        usedTypes.Remove("boolean");

        // Add type imports
        if (usedTypes.Count > 0)
        {
            var typeImports = string.Join(", ", usedTypes.OrderBy(t => t));
            sb.AppendLine($"import type {{ {typeImports} }} from '../types';");
        }

        // Add validation imports
        if (validationImports.Count > 0)
        {
            var validationImportList = string.Join(", ", validationImports.OrderBy(v => v));
            sb.AppendLine($"import {{ {validationImportList} }} from '../validation';");
        }

        sb.AppendLine();

        // Generate functions
        foreach (var endpoint in endpoints)
        {
            var hasValidation = includeValidation && validationRules != null &&
                               endpoint.RequestType != null &&
                               validationRules.ContainsKey(endpoint.RequestType.Name);
            var functionCode = await GenerateApiClientFunctionAsync(endpoint, hasValidation, cancellationToken);
            sb.AppendLine(functionCode);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void ExtractTypeNames(string typeName, HashSet<string> types)
    {
        if (string.IsNullOrEmpty(typeName))
            return;

        // Extract type names from generic types like Result<InitiateLoginResponse>
        // Should extract: Result, InitiateLoginResponse

        var match = System.Text.RegularExpressions.Regex.Match(typeName, @"^([^<]+)<(.+)>$");
        if (match.Success)
        {
            // Add the generic type name (e.g., Result)
            var genericType = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(genericType))
                types.Add(genericType);

            // Add the inner type(s) (e.g., InitiateLoginResponse)
            var innerTypes = match.Groups[2].Value;

            // Handle nested generics and multiple type parameters
            var depth = 0;
            var currentType = new System.Text.StringBuilder();

            foreach (var ch in innerTypes)
            {
                if (ch == '<')
                {
                    depth++;
                    currentType.Append(ch);
                }
                else if (ch == '>')
                {
                    depth--;
                    currentType.Append(ch);
                }
                else if (ch == ',' && depth == 0)
                {
                    // End of current type parameter
                    var typeParam = currentType.ToString().Trim();
                    if (!string.IsNullOrEmpty(typeParam))
                        ExtractTypeNames(typeParam, types);
                    currentType.Clear();
                }
                else
                {
                    currentType.Append(ch);
                }
            }

            // Add the last type
            var lastType = currentType.ToString().Trim();
            if (!string.IsNullOrEmpty(lastType))
                ExtractTypeNames(lastType, types);
        }
        else
        {
            // Simple type name (no generics) - but first clean it up
            var cleanedName = CleanupTypeName(typeName);
            types.Add(cleanedName.Trim());
        }
    }

    private static string CleanupTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;

        // Remove array suffix (User[] -> User)
        typeName = typeName.TrimEnd('[', ']');

        // Remove incomplete generic brackets (e.g., "Result<CompletePasswordResetResponse" -> "Result")
        // Count brackets to see if they're balanced
        var openCount = typeName.Count(c => c == '<');
        var closeCount = typeName.Count(c => c == '>');
        
        if (openCount != closeCount)
        {
            // Malformed - extract just the base type name
            var firstBracket = typeName.IndexOf('<');
            if (firstBracket > 0)
            {
                return typeName.Substring(0, firstBracket);
            }
        }

        return typeName;
    }

    #endregion
}
