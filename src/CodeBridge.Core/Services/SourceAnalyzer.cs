using System.Text.RegularExpressions;
using CodeBridge.Core.Models;
using CodeBridge.Core.Models.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using TypeInfo = CodeBridge.Core.Models.TypeInfo;

namespace CodeBridge.Core.Services;

/// <summary>
/// Analyzes .NET source code using Roslyn to discover API endpoints marked with [GenerateSdk] attribute,
/// types, and validation rules.
/// </summary>
public sealed class SourceAnalyzer(ILogger<SourceAnalyzer> logger, AdvancedOptions? advancedOptions = null) : ISourceAnalyzer
{
    private readonly ILogger<SourceAnalyzer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AdvancedOptions? _advancedOptions = advancedOptions;

    #region Endpoint Discovery

    /// <summary>
    /// Discovers API endpoints marked with [GenerateSdk] attribute.
    /// ONLY methods with this attribute will be included in SDK generation.
    /// </summary>
    public async Task<List<EndpointInfo>> DiscoverEndpointsAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default)
    {
        var endpoints = new List<EndpointInfo>();

        foreach (var project in projects)
        {
            try
            {
                _logger.LogInformation("Discovering [GenerateSdk] endpoints in project: {ProjectName}", project.Name);

                var projectDir = Path.GetDirectoryName(project.Path)!;
                var csharpFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"))
                    .ToList();

                foreach (var file in csharpFiles)
                {
                    var fileEndpoints = await DiscoverEndpointsInFileAsync(file, cancellationToken);
                    endpoints.AddRange(fileEndpoints);
                }

                _logger.LogInformation("Found {Count} endpoints in {ProjectName}",
                    endpoints.Count, project.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering endpoints in project: {ProjectName}", project.Name);
            }
        }

        // Apply exclusion filters
        endpoints = ApplyEndpointFilters(endpoints);

        _logger.LogInformation("Discovered {Count} total endpoints with [GenerateSdk] attribute", endpoints.Count);
        return endpoints;
    }

    private async Task<List<EndpointInfo>> DiscoverEndpointsInFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var endpoints = new List<EndpointInfo>();

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Find all methods with [GenerateSdk] attribute
            var methodsWithAttribute = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(HasGenerateSdkAttribute)
                .ToList();

            if (methodsWithAttribute.Count == 0)
                return endpoints;

            _logger.LogDebug("Found {Count} methods with [GenerateSdk] in {File}",
                methodsWithAttribute.Count, Path.GetFileName(filePath));

            // Analyze each method to extract endpoint information
            foreach (var method in methodsWithAttribute)
            {
                // Check if this is a Minimal API registration method
                if (IsMinimalApiRegistrationMethod(method))
                {
                    var minimalApiEndpoints = ExtractMinimalApiEndpoints(method, syntaxTree, cancellationToken);
                    endpoints.AddRange(minimalApiEndpoints);
                }
                else
                {
                    // Traditional controller endpoint
                    var endpoint = ExtractEndpointFromMethod(method, syntaxTree, cancellationToken);
                    if (endpoint != null)
                    {
                        endpoints.Add(endpoint);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file for [GenerateSdk] endpoints: {File}", filePath);
        }

        return endpoints;
    }

    private static bool HasGenerateSdkAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(IsGenerateSdkAttribute);
    }

    private static bool IsGenerateSdkAttribute(AttributeSyntax attribute)
    {
        var attributeName = attribute.Name.ToString();
        return attributeName == "GenerateSdk" ||
               attributeName == "GenerateSdkAttribute" ||
               attributeName == "CodeBridge.Core.Attributes.GenerateSdk" ||
               attributeName == "CodeBridge.Core.Attributes.GenerateSdkAttribute";
    }

    private EndpointInfo? ExtractEndpointFromMethod(
        MethodDeclarationSyntax method,
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken)
    {
        try
        {
            var methodName = method.Identifier.ValueText;

            var containingClass = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingClass == null)
                return null;

            var className = containingClass.Identifier.ValueText;

            // Try to extract as a controller endpoint first (has HTTP method attributes)
            var endpoint = ExtractControllerEndpoint(method, containingClass);

            // If not a controller endpoint, treat as Minimal API handler
            if (endpoint == null)
            {
                endpoint = ExtractMinimalApiEndpoint(method, className);
            }

            if (endpoint == null)
                return null;

            // Extract metadata from [GenerateSdk] attribute and return updated endpoint
            return ExtractSdkAttributeMetadata(method, endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting endpoint from method: {Method}", method.Identifier.ValueText);
            return null;
        }
    }

    private EndpointInfo? ExtractControllerEndpoint(
        MethodDeclarationSyntax method,
        ClassDeclarationSyntax controller)
    {
        // Extract HTTP method from attributes like [HttpGet], [HttpPost], etc.
        var httpMethodAttribute = method.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().StartsWith("Http"));

        if (httpMethodAttribute == null)
        {
            _logger.LogWarning("Method {Method} has [GenerateSdk] but no HTTP method attribute", method.Identifier.ValueText);
            return null;
        }

        var httpMethod = httpMethodAttribute.Name.ToString().Replace("Http", "").ToUpperInvariant();

        // Extract route from attribute argument or default to method name
        var route = ExtractRouteFromAttribute(httpMethodAttribute) ?? method.Identifier.ValueText;

        // Get controller route prefix from [Route] attribute
        var controllerRoute = ExtractControllerRoute(controller);
        var fullRoute = CombineRoutes(controllerRoute, route);

        // Extract request/response types from method signature
        var (requestType, responseType) = ExtractTypesFromMethodSignature(method);

        var endpoint = new EndpointInfo
        {
            HttpMethod = httpMethod,
            Route = fullRoute,
            ControllerName = controller.Identifier.ValueText.Replace("Controller", ""),
            ActionName = method.Identifier.ValueText,
            RequiresAuthentication = true, // Default, can be overridden by [GenerateSdk] attribute
            RequestType = requestType,
            ResponseType = responseType
        };

        return endpoint;
    }

    private EndpointInfo? ExtractMinimalApiEndpoint(
        MethodDeclarationSyntax method,
        string className)
    {
        // For Minimal API handlers, we need to infer the HTTP method and route
        // This is typically done by looking at where the method is called from (e.g., MapGet, MapPost)
        // For now, we'll use a simple naming convention approach

        var methodName = method.Identifier.ValueText;

        // Try to infer HTTP method from method name (e.g., HandleGetProducts -> GET)
        var httpMethod = InferHttpMethodFromName(methodName);
        if (httpMethod == null)
        {
            _logger.LogWarning("Cannot infer HTTP method for Minimal API handler: {Method}", methodName);
            return null;
        }

        // Infer route from method name (e.g., HandleGetProducts -> /products)
        var route = InferRouteFromMethodName(methodName);

        // Extract request/response types from method signature
        var (requestType, responseType) = ExtractTypesFromMethodSignature(method);

        var endpoint = new EndpointInfo
        {
            HttpMethod = httpMethod,
            Route = route,
            ControllerName = className,
            ActionName = methodName,
            RequiresAuthentication = true,
            RequestType = requestType,
            ResponseType = responseType
        };

        return endpoint;
    }

    private static string? InferHttpMethodFromName(string methodName)
    {
        var lowerName = methodName.ToLowerInvariant();
        if (lowerName.Contains("get")) return "GET";
        if (lowerName.Contains("post") || lowerName.Contains("create")) return "POST";
        if (lowerName.Contains("put") || lowerName.Contains("update")) return "PUT";
        if (lowerName.Contains("delete")) return "DELETE";
        if (lowerName.Contains("patch")) return "PATCH";
        return null;
    }

    private static string InferRouteFromMethodName(string methodName)
    {
        // Remove common prefixes like "Handle", "Execute", etc.
        var cleaned = Regex.Replace(methodName, "^(Handle|Execute|Process)", "", RegexOptions.IgnoreCase);

        // Remove HTTP method names
        cleaned = Regex.Replace(cleaned, "^(Get|Post|Put|Delete|Patch)", "", RegexOptions.IgnoreCase);

        // Convert PascalCase to kebab-case
        cleaned = Regex.Replace(cleaned, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();

        return $"/{cleaned}";
    }

    /// <summary>
    /// Checks if a method is a Minimal API registration method (extension method on IEndpointRouteBuilder with Map{Verb} calls).
    /// </summary>
    private static bool IsMinimalApiRegistrationMethod(MethodDeclarationSyntax method)
    {
        // Check 1: Is this an extension method on IEndpointRouteBuilder or RouteGroupBuilder?
        var firstParam = method.ParameterList.Parameters.FirstOrDefault();
        if (firstParam == null) return false;

        var hasThis = firstParam.Modifiers.Any(m => m.IsKind(SyntaxKind.ThisKeyword));
        var paramType = firstParam.Type?.ToString() ?? "";
        var extendsEndpointBuilder = paramType.Contains("IEndpointRouteBuilder") ||
                                      paramType.Contains("RouteGroupBuilder");

        if (!hasThis || !extendsEndpointBuilder)
            return false;

        // Check 2: Does method body contain Map{Verb} calls?
        var hasMapCalls = method.Body?.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsMapMethodCall) == true;

        return hasMapCalls;
    }

    private static bool IsMapMethodCall(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
            IdentifierNameSyntax id => id.Identifier.ValueText,
            _ => null
        };

        return methodName is "MapGet" or "MapPost" or "MapPut" or "MapDelete" or "MapPatch" or "MapMethods";
    }

    /// <summary>
    /// Extracts multiple endpoints from a Minimal API registration method.
    /// </summary>
    private List<EndpointInfo> ExtractMinimalApiEndpoints(
        MethodDeclarationSyntax registrationMethod,
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken)
    {
        var endpoints = new List<EndpointInfo>();

        try
        {
            var methodBody = registrationMethod.Body;
            if (methodBody == null) return endpoints;

            // Step 1: Find MapGroup() call to get base route
            string? baseRoute = ExtractMapGroupRoute(methodBody);

            // Step 2: Find all Map{Verb} calls
            var mapInvocations = methodBody.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsMapVerbCall)
                .ToList();

            // Step 3: Parse each Map{Verb} invocation
            foreach (var invocation in mapInvocations)
            {
                var endpoint = ParseMapInvocation(invocation, baseRoute, registrationMethod);
                if (endpoint != null)
                {
                    // Apply [GenerateSdk] metadata from the registration method
                    endpoint = ExtractSdkAttributeMetadata(registrationMethod, endpoint);
                    endpoints.Add(endpoint);
                }
            }

            _logger.LogDebug("Extracted {Count} Minimal API endpoints from {Method}",
                endpoints.Count, registrationMethod.Identifier.ValueText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting Minimal API endpoints from {Method}",
                registrationMethod.Identifier.ValueText);
        }

        return endpoints;
    }

    private static bool IsMapVerbCall(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
            _ => null
        };

        return methodName is "MapGet" or "MapPost" or "MapPut" or "MapDelete" or "MapPatch";
    }

    private static string? ExtractMapGroupRoute(BlockSyntax methodBody)
    {
        var groupCall = methodBody.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv =>
            {
                var methodName = inv.Expression switch
                {
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
                    IdentifierNameSyntax id => id.Identifier.ValueText,
                    _ => null
                };
                return methodName == "MapGroup";
            });

        if (groupCall == null) return null;

        // Extract route string from first argument
        var routeArg = groupCall.ArgumentList.Arguments.FirstOrDefault();
        if (routeArg?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    private EndpointInfo? ParseMapInvocation(
        InvocationExpressionSyntax mapInvocation,
        string? baseRoute,
        MethodDeclarationSyntax registrationMethod)
    {
        try
        {
            // Extract HTTP verb
            var httpMethod = ExtractHttpMethod(mapInvocation);
            if (httpMethod == null) return null;

            // Extract route from first argument
            var routeArg = mapInvocation.ArgumentList.Arguments.FirstOrDefault();
            var route = routeArg?.Expression switch
            {
                LiteralExpressionSyntax literal => literal.Token.ValueText,
                MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Empty" } => string.Empty,
                _ => "/"
            };

            // Combine with base route
            var fullRoute = CombineRoutes(baseRoute, route);

            // Extract handler method reference from second argument
            var handlerArg = mapInvocation.ArgumentList.Arguments.ElementAtOrDefault(1);
            var handlerName = handlerArg?.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                _ => null
            };

            // Extract metadata from chained method calls
            var metadata = ExtractEndpointMetadata(mapInvocation);

            // Find the handler method to extract types
            MethodDeclarationSyntax? handlerMethod = null;
            if (handlerName != null)
            {
                handlerMethod = registrationMethod.Parent?.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == handlerName);
            }

            // Extract request/response types
            var (requestType, responseType) = handlerMethod != null
                ? ExtractTypesFromMethodSignature(handlerMethod)
                : (null, metadata.ResponseType);

            return new EndpointInfo
            {
                HttpMethod = httpMethod,
                Route = fullRoute,
                ControllerName = registrationMethod.Parent switch
                {
                    ClassDeclarationSyntax cls => cls.Identifier.ValueText.Replace("Endpoints", ""),
                    _ => "Unknown"
                },
                ActionName = handlerName ?? $"{httpMethod}{route}",
                RequestType = requestType,
                ResponseType = responseType ?? metadata.ResponseType,
                RequiresAuthentication = metadata.RequiresAuth,
                Summary = metadata.Summary,
                Tags = metadata.Tags
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Map invocation");
            return null;
        }
    }

    private static string? ExtractHttpMethod(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
            _ => null
        };

        return methodName switch
        {
            "MapGet" => "GET",
            "MapPost" => "POST",
            "MapPut" => "PUT",
            "MapDelete" => "DELETE",
            "MapPatch" => "PATCH",
            _ => null
        };
    }

    private EndpointMetadata ExtractEndpointMetadata(InvocationExpressionSyntax mapInvocation)
    {
        var metadata = new EndpointMetadata
        {
            RequiresAuth = true, // Default
            Tags = new List<string>()
        };

        // Find all chained method calls
        var current = mapInvocation.Parent;
        while (current != null)
        {
            if (current is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
                {
                    ProcessChainedMethod(chainedInvocation, metadata);
                    current = chainedInvocation.Parent;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        return metadata;
    }

    private void ProcessChainedMethod(InvocationExpressionSyntax invocation, EndpointMetadata metadata)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
            _ => null
        };

        switch (methodName)
        {
            case "WithName":
                metadata.EndpointName = ExtractStringArgument(invocation);
                break;

            case "WithSummary":
                metadata.Summary = ExtractStringArgument(invocation);
                break;

            case "WithTags":
                metadata.Tags.AddRange(ExtractStringArrayArguments(invocation));
                break;

            case "AllowAnonymous":
                metadata.RequiresAuth = false;
                break;

            case "RequireAuthorization":
                metadata.RequiresAuth = true;
                break;

            case "Produces":
                metadata.ResponseType = ExtractProducesType(invocation);
                break;
        }
    }

    private static string? ExtractStringArgument(InvocationExpressionSyntax invocation)
    {
        var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
        return arg?.Expression is LiteralExpressionSyntax literal
            ? literal.Token.ValueText
            : null;
    }

    private static List<string> ExtractStringArrayArguments(InvocationExpressionSyntax invocation)
    {
        var tags = new List<string>();

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal)
            {
                tags.Add(literal.Token.ValueText);
            }
        }

        return tags;
    }

    private TypeInfo? ExtractProducesType(InvocationExpressionSyntax invocation)
    {
        // Extract type from generic method: .Produces<ApiResponse<Result<LoginResponse>>>()
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is GenericNameSyntax genericName)
        {
            var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
            if (typeArg != null)
            {
                var typeName = typeArg.ToString();

                // Strip ApiResponse wrapper: ApiResponse<Result<T>> -> Result<T>
                var innerType = ExtractTypeFromGeneric(typeName, "ApiResponse");

                // Keep Result<T> as-is (it's the actual type name we want)
                var finalType = innerType ?? typeName;

                return new TypeInfo
                {
                    Name = finalType,
                    FullName = finalType,
                    Namespace = null,
                    IsEnum = false,
                    Properties = new List<PropertyInfo>(),
                    IsGeneric = finalType.Contains("<"),
                    GenericArguments = new List<TypeInfo>()
                };
            }
        }

        return null;
    }

    private static EndpointInfo ExtractSdkAttributeMetadata(MethodDeclarationSyntax method, EndpointInfo endpoint)
    {
        var sdkAttribute = method.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(IsGenerateSdkAttribute);

        if (sdkAttribute?.ArgumentList?.Arguments == null)
            return endpoint;

        // Extract attribute properties
        string? summary = endpoint.Summary;
        string? group = null;
        bool requiresAuth = endpoint.RequiresAuthentication;
        List<string> tags = new();
        bool isFileDownload = false;
        bool isFileUpload = false;

        foreach (var argument in sdkAttribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals == null)
                continue;

            var propertyName = argument.NameEquals.Name.Identifier.ValueText;
            var value = ExtractAttributeValue(argument.Expression);

            switch (propertyName)
            {
                case "Summary":
                    if (value is string s)
                        summary = s;
                    break;

                case "Group":
                    if (value is string g)
                        group = g;
                    break;

                case "RequiresAuth":
                    if (value is bool ra)
                        requiresAuth = ra;
                    break;

                case "Tags":
                    if (value is string[] t)
                        tags = t.ToList();
                    break;

                case "IsFileDownload":
                    if (value is bool ifd)
                        isFileDownload = ifd;
                    break;

                case "IsFileUpload":
                    if (value is bool ifu)
                        isFileUpload = ifu;
                    break;
            }
        }

        // Return new EndpointInfo with updated values
        return new EndpointInfo
        {
            HttpMethod = endpoint.HttpMethod,
            Route = endpoint.Route,
            ControllerName = endpoint.ControllerName,
            ActionName = endpoint.ActionName,
            RequestType = endpoint.RequestType,
            ResponseType = endpoint.ResponseType,
            RequiresAuthentication = requiresAuth,
            Summary = summary,
            Group = group,
            Tags = tags,
            IsFileDownload = isFileDownload,
            IsFileUpload = isFileUpload
        };
    }

    private static object? ExtractAttributeValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.StringLiteralToken) => literal.Token.ValueText,
            LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.TrueKeyword) => true,
            LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.FalseKeyword) => false,
            ArrayCreationExpressionSyntax arrayCreation => ExtractStringArray(arrayCreation.Initializer),
            ImplicitArrayCreationExpressionSyntax implicitArray => ExtractStringArray(implicitArray.Initializer),
            _ => null
        };
    }

    private static string[]? ExtractStringArray(InitializerExpressionSyntax? initializer)
    {
        if (initializer?.Expressions == null)
            return null;

        var strings = new List<string>();
        foreach (var expr in initializer.Expressions)
        {
            if (expr is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.StringLiteralToken))
            {
                strings.Add(literal.Token.ValueText);
            }
        }

        return strings.Count > 0 ? strings.ToArray() : null;
    }

    private static string? ExtractControllerRoute(ClassDeclarationSyntax controller)
    {
        var routeAttribute = controller.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Route"));

        return routeAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            _ => null
        };
    }

    private static string? ExtractRouteFromAttribute(AttributeSyntax attribute)
    {
        return attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            _ => null
        };
    }

    private static string CombineRoutes(string? prefix, string route)
    {
        if (string.IsNullOrEmpty(prefix))
            return route.StartsWith('/') ? route : $"/{route}";

        prefix = prefix.TrimEnd('/');
        route = route.TrimStart('/');
        return $"{prefix}/{route}";
    }

    private (TypeInfo? requestType, TypeInfo? responseType) ExtractTypesFromMethodSignature(MethodDeclarationSyntax method)
    {
        TypeInfo? requestType = null;
        TypeInfo? responseType = null;

        // Extract request type from first non-framework parameter
        var parameters = method.ParameterList.Parameters;
        foreach (var param in parameters)
        {
            var paramType = param.Type?.ToString();
            if (string.IsNullOrEmpty(paramType))
                continue;
            if (IsBuiltInType(paramType) || IsFrameworkType(paramType) || paramType.Contains("Dispatcher") || paramType.Contains("CancellationToken"))
                continue;
            requestType = new TypeInfo
            {
                Name = paramType,
                FullName = paramType,
                Namespace = null,
                IsEnum = false,
                Properties = [],
                IsGeneric = false,
                GenericArguments = []
            };
            break;
        }

        // Prefer .Produces<ApiResponse<T>>() metadata if available
        var producesType = method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => a.Name.ToString().Contains("Produces"))
            .Select(a => a.ArgumentList?.Arguments.FirstOrDefault()?.ToString())
            .FirstOrDefault();
        if (!string.IsNullOrEmpty(producesType))
        {
            // Example: typeof(ApiResponse<LoginResponse>)
            var match = Regex.Match(producesType, @"ApiResponse<([\w\.]+)>");
            if (match.Success)
            {
                var typeName = match.Groups[1].Value.Trim();
                responseType = new TypeInfo
                {
                    Name = typeName,
                    FullName = typeName,
                    Namespace = null,
                    IsEnum = false,
                    Properties = [],
                    IsGeneric = false,
                    GenericArguments = []
                };
            }
        }

        // If not found, try dispatcher.SendAsync<Result<T>> pattern
        if (responseType == null)
        {
            var body = method.Body?.ToString();
            if (!string.IsNullOrEmpty(body))
            {
                var sendAsyncPattern = @"SendAsync<([^>]+(?:<[^>]+>)?)>";
                var match = Regex.Match(body, sendAsyncPattern);
                if (match.Success)
                {
                    var dispatcherType = match.Groups[1].Value.Trim();
                    var innerType = ExtractTypeFromGeneric(dispatcherType, "Result");
                    responseType = new TypeInfo
                    {
                        Name = innerType ?? dispatcherType,
                        FullName = innerType ?? dispatcherType,
                        Namespace = null,
                        IsEnum = false,
                        Properties = [],
                        IsGeneric = false,
                        GenericArguments = []
                    };
                }
            }
        }

        // If still not found, fall back to handler return type (unwrapping Task, ActionResult, etc.)
        if (responseType == null)
        {
            var returnType = method.ReturnType?.ToString();
            if (!string.IsNullOrEmpty(returnType))
            {
                var extractedType = ExtractTypeFromGeneric(returnType, "Task", "ActionResult", "IActionResult", "Result");
                if (!string.IsNullOrEmpty(extractedType) && !IsFrameworkType(extractedType) && !IsBuiltInType(extractedType))
                {
                    responseType = new TypeInfo
                    {
                        Name = extractedType,
                        FullName = extractedType,
                        Namespace = null,
                        IsEnum = false,
                        Properties = [],
                        IsGeneric = false,
                        GenericArguments = []
                    };
                }
            }
        }

        return (requestType, responseType);
    }

    private static string? ExtractTypeFromGeneric(string typeString, params string[] wrappers)
    {
        foreach (var wrapper in wrappers)
        {
            // Use a more robust pattern that handles nested generics correctly
            var pattern = $@"^{Regex.Escape(wrapper)}<(.+)>$";
            var match = Regex.Match(typeString, pattern);
            if (match.Success)
            {
                typeString = match.Groups[1].Value.Trim();
                // Only strip one level at a time
                break;
            }
        }

        return string.IsNullOrEmpty(typeString) || typeString == "void" ? null : typeString;
    }

    private static bool IsBuiltInType(string typeName)
    {
        var builtInTypes = new[] { "string", "int", "long", "bool", "DateTime", "Guid", "decimal", "double", "float" };
        return builtInTypes.Contains(typeName);
    }

    private static bool IsFrameworkType(string typeName)
    {
        return typeName.StartsWith("Microsoft.") ||
               typeName.StartsWith("System.") ||
               typeName == "CancellationToken" ||
               typeName == "HttpContext" ||
               typeName == "HttpRequest" ||
               typeName == "HttpResponse";
    }

    private List<EndpointInfo> ApplyEndpointFilters(List<EndpointInfo> endpoints)
    {
        if (_advancedOptions?.ExcludedEndpoints == null || _advancedOptions.ExcludedEndpoints.Count == 0)
            return endpoints;

        return endpoints.Where(e => !IsEndpointExcluded(e.Route)).ToList();
    }

    private bool IsEndpointExcluded(string route)
    {
        if (_advancedOptions?.ExcludedEndpoints == null)
            return false;

        return _advancedOptions.ExcludedEndpoints.Any(pattern =>
        {
            // Simple wildcard pattern matching
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(route, regex, RegexOptions.IgnoreCase);
        });
    }

    #endregion

    #region Type Discovery

    /// <summary>
    /// Discovers all types marked with [SdkInclude] attribute across multiple projects.
    /// </summary>
    /// <param name="projects">List of projects to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered type information, or empty list if none found.</returns>
    public async Task<List<TypeInfo>> DiscoverTypesAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default)
    {
        var types = new List<TypeInfo>();
        var processedTypes = new HashSet<string>();

        foreach (var project in projects)
        {
            try
            {
                _logger.LogInformation("Discovering types in project: {ProjectName}", project.Name);

                var projectDir = Path.GetDirectoryName(project.Path)!;
                var csharpFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"))
                    .ToList();

                foreach (var file in csharpFiles)
                {
                    var fileTypes = await DiscoverTypesInFileAsync(file, processedTypes, cancellationToken);
                    types.AddRange(fileTypes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering types in project: {ProjectName}", project.Name);
            }
        }

        // Apply exclusion filters
        types = ApplyTypeFilters(types);

        _logger.LogInformation("Discovered {Count} types", types.Count);
        return types;
    }

    private async Task<List<TypeInfo>> DiscoverTypesInFileAsync(
        string filePath,
        HashSet<string> processedTypes,
        CancellationToken cancellationToken)
    {
        var types = new List<TypeInfo>();

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Create a compilation for semantic analysis
            var compilation = CSharpCompilation.Create("TempAnalysis")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find all type declarations
            var typeDeclarations = root.DescendantNodes()
                .Where(n => n is ClassDeclarationSyntax or RecordDeclarationSyntax or EnumDeclarationSyntax)
                .ToList();

            foreach (var typeDecl in typeDeclarations)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol == null)
                    continue;

                var fullName = typeSymbol.ToDisplayString();

                // Skip if already processed
                if (processedTypes.Contains(fullName))
                    continue;

                // Check if type should be included
                if (!ShouldIncludeType(fullName))
                    continue;

                processedTypes.Add(fullName);

                var typeInfo = ConvertToTypeInfo(typeSymbol);
                if (typeInfo != null)
                {
                    types.Add(typeInfo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering types in file: {File}", filePath);
        }

        return types;
    }

    private static bool ShouldIncludeType(string fullName)
    {
        // Include types that are likely API-related
        var includePatterns = new[]
        {
            "Command", "Query", "Request", "Response",
            "Dto", "Model", "Result", "Error"
        };

        return includePatterns.Any(pattern => fullName.Contains(pattern));
    }

    private TypeInfo? ConvertToTypeInfo(INamedTypeSymbol typeSymbol)
    {
        try
        {
            var typeName = typeSymbol.Name;
            var fullName = typeSymbol.ToDisplayString();
            var namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();

            // Handle enums
            if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                var enumValues = typeSymbol.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(f => f.IsConst)
                    .Select(f => new EnumValue
                    {
                        Name = f.Name,
                        Value = f.ConstantValue as int? ?? 0
                    })
                    .ToList();

                return new TypeInfo
                {
                    Name = typeName,
                    FullName = fullName,
                    Namespace = namespaceName,
                    IsEnum = true,
                    EnumValues = enumValues,
                    Properties = [],
                    IsGeneric = false,
                    GenericArguments = []
                };
            }

            // Handle regular classes/records
            var properties = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public)
                .Select(ConvertToPropertyInfo)
                .ToList();

            return new TypeInfo
            {
                Name = typeName,
                FullName = fullName,
                Namespace = namespaceName,
                IsEnum = false,
                EnumValues = [],
                Properties = properties,
                IsGeneric = typeSymbol.IsGenericType,
                GenericArguments = typeSymbol.IsGenericType
                    ? typeSymbol.TypeArguments.Select(t => new TypeInfo
                    {
                        Name = t.Name,
                        FullName = t.ToDisplayString(),
                        Namespace = t.ContainingNamespace?.ToDisplayString(),
                        IsEnum = t.TypeKind == TypeKind.Enum,
                        Properties = [],
                        IsGeneric = false,
                        GenericArguments = []
                    }).ToList()
                    : []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting type symbol to TypeInfo: {Type}", typeSymbol.Name);
            return null;
        }
    }

    private static PropertyInfo ConvertToPropertyInfo(IPropertySymbol property)
    {
        var propertyType = property.Type.ToDisplayString();
        var isNullable = property.NullableAnnotation == NullableAnnotation.Annotated;

        var typeInfo = new TypeInfo
        {
            Name = property.Type.Name,
            FullName = propertyType,
            Namespace = property.Type.ContainingNamespace?.ToDisplayString(),
            IsEnum = property.Type.TypeKind == TypeKind.Enum,
            Properties = [],
            IsGeneric = false,
            GenericArguments = []
        };

        return new PropertyInfo
        {
            Name = property.Name,
            Type = typeInfo,
            IsNullable = isNullable,
            IsRequired = !isNullable
        };
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName.StartsWith("System.Collections.Generic.List<") ||
               typeName.StartsWith("System.Collections.Generic.IList<") ||
               typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
               typeName.StartsWith("System.Collections.Generic.ICollection<") ||
               type is IArrayTypeSymbol;
    }

    private List<TypeInfo> ApplyTypeFilters(List<TypeInfo> types)
    {
        if (_advancedOptions?.ExcludedTypes == null || _advancedOptions.ExcludedTypes.Count == 0)
            return types;

        return types.Where(t => !_advancedOptions.ExcludedTypes.Contains(t.Name)).ToList();
    }

    #endregion

    #region Validation Rules Discovery

    /// <summary>
    /// Discovers FluentValidation rules from all validators across multiple projects.
    /// </summary>
    /// <param name="projects">List of projects to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping type names to their validation rules, or empty dictionary if no validators found.</returns>
    public async Task<Dictionary<string, List<PropertyValidationRules>>> DiscoverValidationRulesAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default)
    {
        var validationRules = new Dictionary<string, List<PropertyValidationRules>>();

        foreach (var project in projects)
        {
            try
            {
                _logger.LogInformation("Discovering validation rules in project: {ProjectName}", project.Name);

                var projectDir = Path.GetDirectoryName(project.Path)!;
                var validatorFiles = Directory.GetFiles(projectDir, "*Validator.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"))
                    .ToList();

                foreach (var file in validatorFiles)
                {
                    var fileRules = await ExtractValidationRulesFromFileAsync(file, cancellationToken);
                    foreach (var (typeName, rules) in fileRules)
                    {
                        if (validationRules.ContainsKey(typeName))
                        {
                            validationRules[typeName].AddRange(rules);
                        }
                        else
                        {
                            validationRules[typeName] = rules;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering validation rules in project: {ProjectName}", project.Name);
            }
        }

        _logger.LogInformation("Discovered validation rules for {Count} types", validationRules.Count);
        return validationRules;
    }

    private async Task<Dictionary<string, List<PropertyValidationRules>>> ExtractValidationRulesFromFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var validationRules = new Dictionary<string, List<PropertyValidationRules>>();

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Find validator classes (inherit from AbstractValidator<T>)
            var validatorClasses = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.BaseList?.Types.Any(bt => bt.ToString().Contains("AbstractValidator")) == true)
                .ToList();

            foreach (var validatorClass in validatorClasses)
            {
                var typeName = ExtractValidatedTypeName(validatorClass);
                if (string.IsNullOrEmpty(typeName))
                    continue;

                var rules = ExtractPropertyValidationRules(validatorClass);
                if (rules.Count > 0)
                {
                    validationRules[typeName] = rules;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting validation rules from file: {File}", filePath);
        }

        return validationRules;
    }

    private static string? ExtractValidatedTypeName(ClassDeclarationSyntax validatorClass)
    {
        var baseType = validatorClass.BaseList?.Types.FirstOrDefault(bt => bt.ToString().Contains("AbstractValidator"));
        if (baseType == null)
            return null;

        var match = Regex.Match(baseType.ToString(), @"AbstractValidator<(.+?)>");
        return match.Success ? match.Groups[1].Value : null;
    }

    private List<PropertyValidationRules> ExtractPropertyValidationRules(ClassDeclarationSyntax validatorClass)
    {
        var rules = new List<PropertyValidationRules>();

        // Find RuleFor statements in constructor or methods
        var invocations = validatorClass.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression.ToString().Contains("RuleFor"))
            .ToList();

        foreach (var invocation in invocations)
        {
            var propertyRule = ParseRuleForStatement(invocation);
            if (propertyRule != null)
            {
                rules.Add(propertyRule);
            }
        }

        return rules;
    }

    private PropertyValidationRules? ParseRuleForStatement(InvocationExpressionSyntax invocation)
    {
        try
        {
            // Extract property name from RuleFor(x => x.PropertyName)
            var propertyName = ExtractPropertyNameFromRuleFor(invocation);
            if (string.IsNullOrEmpty(propertyName))
                return null;

            bool? required = null;
            int? minLength = null;
            int? maxLength = null;
            string? pattern = null;
            bool? email = null;

            // Parse validation chain (e.g., .NotEmpty().MaximumLength(100))
            var chainedCalls = GetChainedMethodCalls(invocation);
            foreach (var call in chainedCalls)
            {
                var methodName = call.ToString();

                if (methodName.Contains("NotEmpty") || methodName.Contains("NotNull"))
                {
                    required = true;
                }
                else if (methodName.Contains("MinimumLength"))
                {
                    minLength = ExtractNumericArgument(call);
                }
                else if (methodName.Contains("MaximumLength"))
                {
                    maxLength = ExtractNumericArgument(call);
                }
                else if (methodName.Contains("EmailAddress"))
                {
                    email = true;
                }
                else if (methodName.Contains("Matches"))
                {
                    pattern = ExtractStringArgument(call);
                }
            }

            return new PropertyValidationRules
            {
                PropertyName = propertyName,
                Required = required,
                MinLength = minLength,
                MaxLength = maxLength,
                Pattern = pattern,
                Email = email
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing RuleFor statement");
            return null;
        }
    }

    private static string? ExtractPropertyNameFromRuleFor(InvocationExpressionSyntax invocation)
    {
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (argument?.Expression is not SimpleLambdaExpressionSyntax lambda)
            return null;

        // Extract from x => x.PropertyName
        var match = Regex.Match(lambda.ToString(), @"=>\s*\w+\.(\w+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static List<InvocationExpressionSyntax> GetChainedMethodCalls(InvocationExpressionSyntax invocation)
    {
        var calls = new List<InvocationExpressionSyntax> { invocation };

        var current = invocation.Parent;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            current = memberAccess.Parent;
            if (current is InvocationExpressionSyntax chainedInvocation)
            {
                calls.Add(chainedInvocation);
                current = chainedInvocation.Parent;
            }
            else
            {
                break;
            }
        }

        return calls;
    }

    private static int? ExtractNumericArgument(InvocationExpressionSyntax invocation)
    {
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (argument?.Expression is LiteralExpressionSyntax literal &&
            int.TryParse(literal.Token.ValueText, out var value))
        {
            return value;
        }

        return null;
    }

    #endregion

    #region Parameter Extraction

    /// <summary>
    /// Extracts parameter information from an endpoint's route and body.
    /// Handles route parameters, query parameters, and request body types.
    /// </summary>
    /// <param name="endpoint">The endpoint to extract parameters from.</param>
    /// <param name="availableTypes">List of available types for resolving parameter types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of parameter information including name, type, location, and whether it's required.</returns>
    public Task<List<ParameterInfo>> ExtractParametersAsync(
        EndpointInfo endpoint,
        List<TypeInfo> availableTypes,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<ParameterInfo>();

        // Extract route parameters (e.g., {id}, {customerId})
        var routeParams = Regex.Matches(endpoint.Route, @"\{(\w+)\}")
            .Select(m => new ParameterInfo
            {
                Name = m.Groups[1].Value,
                Type = new TypeInfo
                {
                    Name = "string",
                    FullName = "string",
                    Namespace = null,
                    IsEnum = false,
                    Properties = [],
                    IsGeneric = false,
                    GenericArguments = []
                },
                IsRequired = true,
                Source = ParameterSource.Route
            });

        parameters.AddRange(routeParams);

        // Add request body parameter if request type is specified
        if (endpoint.RequestType != null)
        {
            parameters.Add(new ParameterInfo
            {
                Name = ToCamelCase(endpoint.RequestType.Name),
                Type = endpoint.RequestType,
                IsRequired = true,
                Source = ParameterSource.Body
            });
        }

        return Task.FromResult(parameters);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    #endregion
}

/// <summary>
/// Helper class to accumulate endpoint metadata during parsing.
/// </summary>
internal class EndpointMetadata
{
    public string? EndpointName { get; set; }
    public string? Summary { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool RequiresAuth { get; set; } = true;
    public TypeInfo? ResponseType { get; set; }
}
