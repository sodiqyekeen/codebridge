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
                var endpoint = ExtractEndpointFromMethod(method, syntaxTree, cancellationToken);
                if (endpoint != null)
                {
                    endpoints.Add(endpoint);
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

        // Extract response type from return type
        var returnType = method.ReturnType?.ToString();
        if (!string.IsNullOrEmpty(returnType))
        {
            var extractedType = ExtractTypeFromGeneric(returnType, "Task", "ActionResult", "IActionResult", "Result");
            if (!string.IsNullOrEmpty(extractedType))
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

        // Extract request type from parameters
        var parameters = method.ParameterList.Parameters;
        foreach (var param in parameters)
        {
            var paramType = param.Type?.ToString();
            if (string.IsNullOrEmpty(paramType))
                continue;

            // Skip built-in types and framework types
            if (IsBuiltInType(paramType) || IsFrameworkType(paramType))
                continue;

            // Check for [FromBody] attribute
            var hasFromBody = param.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString().Contains("FromBody"));

            if (hasFromBody || parameters.Count == 1)
            {
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
        }

        return (requestType, responseType);
    }

    private static string? ExtractTypeFromGeneric(string typeString, params string[] wrappers)
    {
        foreach (var wrapper in wrappers)
        {
            var pattern = $@"{wrapper}<(.+?)>";
            var match = Regex.Match(typeString, pattern);
            if (match.Success)
            {
                typeString = match.Groups[1].Value.Trim();
            }
        }

        // Remove any remaining generic wrappers
        typeString = Regex.Replace(typeString, @"^[^<]+<(.+)>$", "$1").Trim();

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

    private static string? ExtractStringArgument(InvocationExpressionSyntax invocation)
    {
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (argument?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
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
