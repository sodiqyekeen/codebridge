using System.Text;
using CodeBridge.Core.Models;
using CodeBridge.Core.Models.Configuration;

namespace CodeBridge.Core.Services;

/// <summary>
/// Generates comprehensive README.md documentation for generated SDKs.
/// Includes installation, configuration, usage examples, and API reference.
/// </summary>
public class ReadmeGenerator
{
    /// <summary>
    /// Generates README.md content based on SDK configuration and discovered endpoints.
    /// </summary>
    public string Generate(
        CodeBridgeConfig config,
        List<EndpointInfo> endpoints,
        List<TypeInfo> types,
        List<ValidationRule> validationRules)
    {
        var sb = new StringBuilder();

        // Title and description
        GenerateHeader(sb, config);

        // Badges (if available)
        GenerateBadges(sb, config);

        // Table of Contents
        GenerateTableOfContents(sb, endpoints);

        // Installation
        GenerateInstallation(sb, config);

        // Quick Start
        GenerateQuickStart(sb, config, endpoints);

        // Configuration
        GenerateConfiguration(sb, config);

        // Features
        GenerateFeatures(sb, config, validationRules);

        // API Reference
        GenerateApiReference(sb, endpoints);

        // Usage Examples
        GenerateUsageExamples(sb, endpoints, types);

        // Error Handling
        GenerateErrorHandling(sb);

        // TypeScript Support
        GenerateTypeScriptInfo(sb);

        // Contributing (if enabled)
        if (config.Output.IncludeContributing)
        {
            GenerateContributing(sb);
        }

        // License
        GenerateLicense(sb, config);

        return sb.ToString();
    }

    private void GenerateHeader(StringBuilder sb, CodeBridgeConfig config)
    {
        sb.AppendLine($"# {config.Output.PackageName}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(config.Output.Description))
        {
            sb.AppendLine(config.Output.Description);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(config.Api.BaseUrl))
        {
            sb.AppendLine($"**API Base URL**: `{config.Api.BaseUrl}`");
            sb.AppendLine();
        }
    }

    private void GenerateBadges(StringBuilder sb, CodeBridgeConfig config)
    {
        var badges = new List<string>();

        // Version badge
        if (!string.IsNullOrEmpty(config.Output.Version))
        {
            badges.Add($"![Version](https://img.shields.io/badge/version-{config.Output.Version}-blue)");
        }

        // TypeScript badge
        badges.Add("![TypeScript](https://img.shields.io/badge/TypeScript-5.0+-blue)");

        // License badge
        if (!string.IsNullOrEmpty(config.Output.License))
        {
            badges.Add($"![License](https://img.shields.io/badge/license-{config.Output.License}-green)");
        }

        if (badges.Any())
        {
            sb.AppendLine(string.Join(" ", badges));
            sb.AppendLine();
        }
    }

    private void GenerateTableOfContents(StringBuilder sb, List<EndpointInfo> endpoints)
    {
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();
        sb.AppendLine("- [Installation](#installation)");
        sb.AppendLine("- [Quick Start](#quick-start)");
        sb.AppendLine("- [Configuration](#configuration)");
        sb.AppendLine("- [Features](#features)");
        sb.AppendLine("- [API Reference](#api-reference)");

        var groups = endpoints.GroupBy(e => e.Group ?? "General").OrderBy(g => g.Key);
        foreach (var group in groups)
        {
            var anchor = group.Key.ToLowerInvariant().Replace(" ", "-");
            sb.AppendLine($"  - [{group.Key}](#{anchor})");
        }

        sb.AppendLine("- [Usage Examples](#usage-examples)");
        sb.AppendLine("- [Error Handling](#error-handling)");
        sb.AppendLine("- [TypeScript Support](#typescript-support)");
        sb.AppendLine("- [License](#license)");
        sb.AppendLine();
    }

    private void GenerateInstallation(StringBuilder sb, CodeBridgeConfig config)
    {
        sb.AppendLine("## Installation");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"npm install {config.Output.PackageName}");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("Or with yarn:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"yarn add {config.Output.PackageName}");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("Or with pnpm:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"pnpm add {config.Output.PackageName}");
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private void GenerateQuickStart(StringBuilder sb, CodeBridgeConfig config, List<EndpointInfo> endpoints)
    {
        sb.AppendLine("## Quick Start");
        sb.AppendLine();

        var exampleEndpoint = endpoints.FirstOrDefault(e => !e.RequiresAuthentication)
                           ?? endpoints.FirstOrDefault();

        if (exampleEndpoint != null)
        {
            sb.AppendLine("```typescript");
            sb.AppendLine($"import {{ configureSdk, {exampleEndpoint.FunctionName} }} from '{config.Output.PackageName}';");
            sb.AppendLine();
            sb.AppendLine("// Configure the SDK");
            sb.AppendLine("configureSdk({");
            sb.AppendLine($"  baseUrl: '{config.Api.BaseUrl}',");
            sb.AppendLine("});");
            sb.AppendLine();
            sb.AppendLine("// Make API calls");
            sb.AppendLine($"const result = await {exampleEndpoint.FunctionName}();");
            sb.AppendLine("if (result.success) {");
            sb.AppendLine("  console.log(result.data);");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
        }
    }

    private void GenerateConfiguration(StringBuilder sb, CodeBridgeConfig config)
    {
        sb.AppendLine("## Configuration");
        sb.AppendLine();
        sb.AppendLine("Configure the SDK before making any API calls:");
        sb.AppendLine();

        sb.AppendLine("```typescript");
        sb.AppendLine($"import {{ configureSdk }} from '{config.Output.PackageName}';");
        sb.AppendLine();
        sb.AppendLine("configureSdk({");
        sb.AppendLine($"  baseUrl: '{config.Api.BaseUrl}',");
        sb.AppendLine("  timeout: 30000, // Optional: request timeout in milliseconds");
        sb.AppendLine("  headers: { // Optional: custom headers");
        sb.AppendLine("    'X-Custom-Header': 'value',");
        sb.AppendLine("  },");

        if (config.Features.IncludeCsrf)
        {
            sb.AppendLine("  csrf: { // Optional: CSRF protection");
            sb.AppendLine("    enabled: true,");
            sb.AppendLine("    tokenEndpoint: '/api/csrf/token',");
            sb.AppendLine("    tokenExpiry: 3600000, // 1 hour");
            sb.AppendLine("  },");
        }

        sb.AppendLine("  auth: { // Optional: authentication");
        sb.AppendLine("    getAccessToken: () => localStorage.getItem('accessToken'),");
        sb.AppendLine("    getRefreshToken: () => localStorage.getItem('refreshToken'),");
        sb.AppendLine("    refreshEndpoint: '/api/auth/refresh',");
        sb.AppendLine("    onTokenRefreshed: (accessToken, refreshToken) => {");
        sb.AppendLine("      localStorage.setItem('accessToken', accessToken);");
        sb.AppendLine("      if (refreshToken) {");
        sb.AppendLine("        localStorage.setItem('refreshToken', refreshToken);");
        sb.AppendLine("      }");
        sb.AppendLine("    },");
        sb.AppendLine("    extractSessionId: true, // Extract session ID from JWT");
        sb.AppendLine("  },");
        sb.AppendLine("  onUnauthorized: () => {");
        sb.AppendLine("    // Handle 401 errors");
        sb.AppendLine("    window.location.href = '/login';");
        sb.AppendLine("  },");
        sb.AppendLine("  onForbidden: () => {");
        sb.AppendLine("    // Handle 403 errors");
        sb.AppendLine("    console.error('Access forbidden');");
        sb.AppendLine("  },");
        sb.AppendLine("});");
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private void GenerateFeatures(StringBuilder sb, CodeBridgeConfig config, List<ValidationRule> validationRules)
    {
        sb.AppendLine("## Features");
        sb.AppendLine();

        var features = new List<string>
        {
            "✅ **Full TypeScript Support** - Complete type definitions for all API endpoints",
            "✅ **React Hooks** - Ready-to-use hooks for React applications",
            "✅ **Automatic Error Handling** - Built-in error handling with Problem Details (RFC 7807) support",
            "✅ **Request/Response Types** - Strongly-typed request and response models"
        };

        if (config.Features.IncludeCsrf)
        {
            features.Add("✅ **CSRF Protection** - Automatic CSRF token management with caching");
        }

        features.Add("✅ **Token Refresh** - Intelligent token refresh with retry protection and cooldown");
        features.Add("✅ **File Operations** - Simplified file upload and download utilities");

        if (validationRules.Any())
        {
            features.Add("✅ **Client-Side Validation** - Validation schemas generated from FluentValidation rules");
        }

        features.Add("✅ **Configurable Timeouts** - Customizable request timeouts");
        features.Add("✅ **Custom Headers** - Easy header management");

        foreach (var feature in features)
        {
            sb.AppendLine(feature);
        }

        sb.AppendLine();
    }

    private void GenerateApiReference(StringBuilder sb, List<EndpointInfo> endpoints)
    {
        sb.AppendLine("## API Reference");
        sb.AppendLine();

        var groups = endpoints
            .GroupBy(e => e.Group ?? "General")
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            sb.AppendLine($"### {group.Key}");
            sb.AppendLine();

            var endpointList = group.OrderBy(e => e.Route).ToList();

            foreach (var endpoint in endpointList)
            {
                sb.AppendLine($"#### `{endpoint.FunctionName}`");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(endpoint.Summary))
                {
                    sb.AppendLine(endpoint.Summary);
                    sb.AppendLine();
                }

                sb.AppendLine($"**HTTP Method**: `{endpoint.HttpMethod}`  ");
                sb.AppendLine($"**Route**: `{endpoint.Route}`  ");

                if (endpoint.RequiresAuthentication)
                {
                    sb.AppendLine($"**Requires Authentication**: Yes  ");
                }

                if (endpoint.Tags.Any())
                {
                    sb.AppendLine($"**Tags**: {string.Join(", ", endpoint.Tags.Select(t => $"`{t}`"))}  ");
                }

                sb.AppendLine();
            }
        }
    }

    private void GenerateUsageExamples(StringBuilder sb, List<EndpointInfo> endpoints, List<TypeInfo> types)
    {
        sb.AppendLine("## Usage Examples");
        sb.AppendLine();

        var groups = endpoints
            .GroupBy(e => e.Group ?? "General")
            .OrderBy(g => g.Key)
            .Take(3); // Show examples for first 3 groups

        foreach (var group in groups)
        {
            sb.AppendLine($"### {group.Key} Examples");
            sb.AppendLine();

            var exampleEndpoint = group.First();

            // Regular function usage
            sb.AppendLine("**Using the function directly:**");
            sb.AppendLine();
            sb.AppendLine("```typescript");
            sb.AppendLine($"import {{ {exampleEndpoint.FunctionName} }} from '{group.Key.ToLowerInvariant()}';");
            sb.AppendLine();

            if (exampleEndpoint.RequestType != null)
            {
                var requestType = types.FirstOrDefault(t => t.Name == exampleEndpoint.RequestType.Name);
                sb.AppendLine($"const request = {{");

                if (requestType != null)
                {
                    foreach (var prop in requestType.Properties.Take(3))
                    {
                        var exampleValue = GetExampleValue(prop.Type.Name);
                        sb.AppendLine($"  {ToCamelCase(prop.Name)}: {exampleValue},");
                    }
                }

                sb.AppendLine("};");
                sb.AppendLine();
            }

            sb.AppendLine($"const result = await {exampleEndpoint.FunctionName}(");
            if (exampleEndpoint.RequestType != null)
            {
                sb.AppendLine("  request");
            }
            sb.AppendLine(");");
            sb.AppendLine();
            sb.AppendLine("if (result.success) {");
            sb.AppendLine("  console.log('Success:', result.data);");
            sb.AppendLine("} else {");
            sb.AppendLine("  console.error('Error:', result.error);");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();

            // React hook usage
            sb.AppendLine("**Using React hook:**");
            sb.AppendLine();
            sb.AppendLine("```tsx");
            sb.AppendLine($"import {{ use{CapitalizeFirst(exampleEndpoint.FunctionName)}Async }} from 'hooks';");
            sb.AppendLine();
            sb.AppendLine("function MyComponent() {");
            sb.AppendLine($"  const {{ mutateAsync, isPending, error }} = use{CapitalizeFirst(exampleEndpoint.FunctionName)}Async();");
            sb.AppendLine();
            sb.AppendLine("  const handleSubmit = async () => {");

            if (exampleEndpoint.RequestType != null)
            {
                sb.AppendLine("    const result = await mutateAsync(request);");
            }
            else
            {
                sb.AppendLine("    const result = await mutateAsync();");
            }

            sb.AppendLine("    if (result.success) {");
            sb.AppendLine("      // Handle success");
            sb.AppendLine("    }");
            sb.AppendLine("  };");
            sb.AppendLine();
            sb.AppendLine("  return (");
            sb.AppendLine("    <button onClick={handleSubmit} disabled={isPending}>");
            sb.AppendLine("      {isPending ? 'Loading...' : 'Submit'}");
            sb.AppendLine("    </button>");
            sb.AppendLine("  );");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
        }
    }

    private void GenerateErrorHandling(StringBuilder sb)
    {
        sb.AppendLine("## Error Handling");
        sb.AppendLine();
        sb.AppendLine("All API functions return a `Result<T>` type:");
        sb.AppendLine();
        sb.AppendLine("```typescript");
        sb.AppendLine("type Result<T> = {");
        sb.AppendLine("  success: true;");
        sb.AppendLine("  data: T;");
        sb.AppendLine("} | {");
        sb.AppendLine("  success: false;");
        sb.AppendLine("  error: string;");
        sb.AppendLine("  statusCode?: number;");
        sb.AppendLine("  problemDetails?: ProblemDetails;");
        sb.AppendLine("};");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**Example error handling:**");
        sb.AppendLine();
        sb.AppendLine("```typescript");
        sb.AppendLine("const result = await someApiCall();");
        sb.AppendLine();
        sb.AppendLine("if (!result.success) {");
        sb.AppendLine("  // Handle error");
        sb.AppendLine("  console.error(result.error);");
        sb.AppendLine("  ");
        sb.AppendLine("  // Access Problem Details if available (RFC 7807)");
        sb.AppendLine("  if (result.problemDetails) {");
        sb.AppendLine("    console.error('Type:', result.problemDetails.type);");
        sb.AppendLine("    console.error('Title:', result.problemDetails.title);");
        sb.AppendLine("    console.error('Status:', result.problemDetails.status);");
        sb.AppendLine("  }");
        sb.AppendLine("  return;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// Use result.data");
        sb.AppendLine("console.log(result.data);");
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private void GenerateTypeScriptInfo(StringBuilder sb)
    {
        sb.AppendLine("## TypeScript Support");
        sb.AppendLine();
        sb.AppendLine("This SDK is written in TypeScript and provides complete type definitions for:");
        sb.AppendLine();
        sb.AppendLine("- All API endpoints");
        sb.AppendLine("- Request and response models");
        sb.AppendLine("- Configuration options");
        sb.AppendLine("- Error types");
        sb.AppendLine("- React hooks");
        sb.AppendLine();
        sb.AppendLine("**TypeScript 5.0+** is recommended for the best experience.");
        sb.AppendLine();
    }

    private void GenerateContributing(StringBuilder sb)
    {
        sb.AppendLine("## Contributing");
        sb.AppendLine();
        sb.AppendLine("Contributions are welcome! Please feel free to submit a Pull Request.");
        sb.AppendLine();
    }

    private void GenerateLicense(StringBuilder sb, CodeBridgeConfig config)
    {
        sb.AppendLine("## License");
        sb.AppendLine();
        sb.AppendLine($"This project is licensed under the {config.Output.License ?? "MIT"} License.");
        sb.AppendLine();
    }

    private string GetExampleValue(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "string" => "'example'",
            "number" or "int" or "long" or "decimal" or "double" or "float" => "123",
            "boolean" or "bool" => "true",
            "date" or "datetime" => "new Date()",
            "guid" => "'00000000-0000-0000-0000-000000000000'",
            _ => "'value'"
        };
    }

    private string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private string CapitalizeFirst(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
