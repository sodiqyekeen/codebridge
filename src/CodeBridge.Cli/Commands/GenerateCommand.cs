using System.CommandLine;
using System.Diagnostics;
using System.Text;
using CodeBridge.Core.Models;
using CodeBridge.Core.Services;
using Microsoft.Extensions.Logging;

namespace CodeBridge.Cli.Commands;

/// <summary>
/// Command to generate TypeScript SDK from .NET API.
/// </summary>
public sealed class GenerateCommand : Command
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCommand"/> class.
    /// Configures options for configuration file path, verbose logging, and watch mode.
    /// </summary>
    public GenerateCommand() : base("generate", "Generate TypeScript SDK from .NET API")
    {
        var configOption = new Option<string>(
            aliases: new[] { "--config", "-c" },
            description: "Path to codebridge.json configuration file",
            getDefaultValue: () => "./codebridge.json");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose logging",
            getDefaultValue: () => false);

        var watchOption = new Option<bool>(
            aliases: new[] { "--watch", "-w" },
            description: "Watch for changes and regenerate",
            getDefaultValue: () => false);

        AddOption(configOption);
        AddOption(verboseOption);
        AddOption(watchOption);

        this.SetHandler(async (context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var watch = context.ParseResult.GetValueForOption(watchOption);

            await ExecuteAsync(configPath, verbose, watch, context.GetCancellationToken());
        });
    }

    private static async Task ExecuteAsync(
        string configPath,
        bool verbose,
        bool watch,
        CancellationToken cancellationToken)
    {
        var logLevel = verbose ? LogLevel.Debug : LogLevel.Information;
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(logLevel);
            builder.AddConsole();
        });

        var logger = loggerFactory.CreateLogger<GenerateCommand>();

        try
        {
            // Load configuration
            logger.LogInformation("📖 Loading configuration from {ConfigPath}...", configPath);
            var configLoader = new ConfigurationLoader(loggerFactory.CreateLogger<ConfigurationLoader>());
            var config = await configLoader.LoadAsync(configPath, cancellationToken);

            if (config == null)
            {
                logger.LogError("❌ Failed to load configuration file: {ConfigPath}", configPath);
                return;
            }

            logger.LogInformation("✅ Configuration loaded successfully");

            // Run generation
            await RunGenerationAsync(config, loggerFactory, cancellationToken);

            if (watch)
            {
                logger.LogInformation("\n👀 Watching for changes... Press Ctrl+C to exit\n");
                await WatchForChangesAsync(configPath, loggerFactory, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("\n⚠️  Generation cancelled by user");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during SDK generation");
            throw;
        }
    }

    private static async Task RunGenerationAsync(
        Core.Models.Configuration.CodeBridgeConfig config,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<GenerateCommand>();
        var stopwatch = Stopwatch.StartNew();
        var codeBridgeVersion = GetCodeBridgeVersion();

        logger.LogInformation("\n🚀 Starting SDK generation (CodeBridge v{Version})...\n", codeBridgeVersion);

        // Phase 1: Source Analysis
        logger.LogInformation("📊 Phase 1/4: Analyzing source code...");
        var sourceAnalyzer = new SourceAnalyzer(loggerFactory.CreateLogger<SourceAnalyzer>());

        // Build project list
        var projects = new List<Core.Models.ProjectInfo>();
        if (!string.IsNullOrEmpty(config.SolutionPath))
        {
            // TODO: Parse solution file to get projects
            logger.LogWarning("Solution file parsing not yet implemented, using direct project paths");
        }

        if (config.ProjectPaths.Count == 0)
        {
            // Default to current directory
            config.ProjectPaths.Add(Directory.GetCurrentDirectory());
        }

        foreach (var projectPath in config.ProjectPaths)
        {
            var fullPath = Path.GetFullPath(projectPath);
            projects.Add(new Core.Models.ProjectInfo
            {
                Name = Path.GetFileName(fullPath),
                Path = fullPath
            });
        }

        var endpoints = await sourceAnalyzer.DiscoverEndpointsAsync(projects, cancellationToken);
        var types = await sourceAnalyzer.DiscoverTypesAsync(projects, cancellationToken);

        // Add types referenced in endpoint response types that weren't discovered
        var referencedTypes = await ExtractReferencedTypesFromEndpointsAsync(endpoints, types, sourceAnalyzer, projects, cancellationToken);
        types.AddRange(referencedTypes);

        // Extract parameters for each endpoint (route params, query params, body)
        foreach (var endpoint in endpoints)
        {
            endpoint.Parameters = await sourceAnalyzer.ExtractParametersAsync(endpoint, types, cancellationToken);
        }

        logger.LogInformation("   ✓ Found {EndpointCount} endpoints", endpoints.Count);
        logger.LogInformation("   ✓ Found {TypeCount} types", types.Count);

        // Phase 2: Validation Analysis (if enabled)
        Dictionary<string, List<Core.Models.PropertyValidationRules>>? validationRules = null;
        if (config.Features.IncludeValidation)
        {
            logger.LogInformation("\n📋 Phase 2/4: Analyzing validation rules...");
            validationRules = await sourceAnalyzer.DiscoverValidationRulesAsync(projects, cancellationToken);
            logger.LogInformation("   ✓ Found validation rules for {Count} types", validationRules.Count);
        }
        else
        {
            logger.LogInformation("\n⏭️  Phase 2/4: Skipped (validation disabled)");
        }

        // Phase 3: Code Generation
        logger.LogInformation("\n✨ Phase 3/4: Generating TypeScript code...");

        var outputPath = Path.GetFullPath(config.Output.Path);
        if (config.Output.CleanBeforeGenerate && Directory.Exists(outputPath))
        {
            logger.LogInformation("   🧹 Cleaning output directory...");
            Directory.Delete(outputPath, recursive: true);
        }

        Directory.CreateDirectory(outputPath);

        var typeMapper = new TypeMapper(
            loggerFactory.CreateLogger<TypeMapper>(),
            config.Advanced);

        var codeGenerator = new CodeGenerator(
            loggerFactory.CreateLogger<CodeGenerator>(),
            typeMapper,
            config.Target,
            config.Features);

        // Generate types
        var typesDir = Path.Combine(outputPath, "types");
        Directory.CreateDirectory(typesDir);

        // Generate built-in Result<T> type first
        var resultTypeContent = @"/**
 * Generic result wrapper for API responses
 */
export interface Result<T> {
  value: T;
  isSuccess: boolean;
  error?: {
    message: string;
    code?: string;
    details?: Record<string, any>;
  };
}
";
        await File.WriteAllTextAsync(Path.Combine(typesDir, "result.ts"), resultTypeContent, cancellationToken);

        foreach (var type in types)
        {
            var content = type.IsEnum
                ? await codeGenerator.GenerateTypeScriptEnumAsync(type, cancellationToken)
                : await codeGenerator.GenerateTypeScriptInterfaceAsync(type, cancellationToken);

            var fileName = $"{ToCamelCase(SanitizeFileName(type.Name))}.ts";
            await File.WriteAllTextAsync(Path.Combine(typesDir, fileName), content, cancellationToken);
        }

        logger.LogInformation("   ✓ Generated {Count} type files", types.Count);

        // Generate types index.ts (barrel export)
        var typeExports = new List<string> { "result" }; // Always include Result<T>
        typeExports.AddRange(types.Select(t => ToCamelCase(SanitizeFileName(t.Name))));
        var typesIndexContent = await codeGenerator.GenerateBarrelExportAsync(typeExports, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(typesDir, "index.ts"), typesIndexContent, cancellationToken);
        logger.LogInformation("   ✓ Generated types/index.ts");

        // Generate HTTP service
        var libDir = Path.Combine(outputPath, "lib");
        Directory.CreateDirectory(libDir);
        var httpServiceCode = await codeGenerator.GenerateHttpServiceAsync(cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(libDir, "httpService.ts"), httpServiceCode, cancellationToken);
        logger.LogInformation("   ✓ Generated HTTP service");

        // Generate lib/index.ts barrel export
        var libIndexContent = await codeGenerator.GenerateBarrelExportAsync(new List<string> { "httpService" }, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(libDir, "index.ts"), libIndexContent, cancellationToken);
        logger.LogInformation("   ✓ Generated lib/index.ts");

        // Generate API client
        var apiDir = Path.Combine(outputPath, "api");
        Directory.CreateDirectory(apiDir);

        var groupedEndpoints = endpoints.GroupBy(e => e.Group ?? "general");
        foreach (var group in groupedEndpoints)
        {
            var groupFileName = $"{ToCamelCase(group.Key)}.ts";
            var groupContent = await codeGenerator.GenerateApiClientFileAsync(
                group.Key,
                group.ToList(),
                config.Features.IncludeValidation,
                validationRules,
                cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(apiDir, groupFileName), groupContent, cancellationToken);
        }

        // Generate API index.ts (barrel export)
        var apiExports = groupedEndpoints.Select(g => ToCamelCase(g.Key)).ToList();
        var apiIndexContent = await codeGenerator.GenerateBarrelExportAsync(apiExports, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(apiDir, "index.ts"), apiIndexContent, cancellationToken);
        logger.LogInformation("   ✓ Generated {Count} API client files + index.ts", groupedEndpoints.Count());

        // Generate validation schemas (if enabled)
        var hasValidationIndex = false;
        if (config.Features.IncludeValidation && validationRules != null)
        {
            var validationDir = Path.Combine(outputPath, "validation");
            Directory.CreateDirectory(validationDir);

            var generatedSchemas = new List<string>(); // Track which schemas were actually generated

            foreach (var type in types.Where(t => !t.IsEnum))
            {
                var schema = await codeGenerator.GenerateValidationSchemaAsync(
                    type,
                    validationRules,
                    cancellationToken);

                if (!string.IsNullOrEmpty(schema))
                {
                    var fileName = $"{ToCamelCase(type.Name)}.schema.ts";
                    await File.WriteAllTextAsync(Path.Combine(validationDir, fileName), schema, cancellationToken);
                    generatedSchemas.Add($"{ToCamelCase(type.Name)}.schema");
                }
            }

            logger.LogInformation("   ✓ Generated validation schemas");

            // Generate validation/index.ts barrel export - only for types with actual schemas
            if (generatedSchemas.Count > 0)
            {
                var validationIndexContent = await codeGenerator.GenerateBarrelExportAsync(generatedSchemas, cancellationToken);
                await File.WriteAllTextAsync(Path.Combine(validationDir, "index.ts"), validationIndexContent, cancellationToken);
                logger.LogInformation("   ✓ Generated validation/index.ts");
                hasValidationIndex = true;
            }
        }

        // Generate React hooks (if enabled)
        if (config.Features.GenerateReactHooks)
        {
            logger.LogInformation("   ⚛️  Generating React hooks...");
            var hooksDir = Path.Combine(outputPath, "hooks");
            Directory.CreateDirectory(hooksDir);

            foreach (var endpoint in endpoints)
            {
                var hookCode = await codeGenerator.GenerateReactHookAsync(endpoint, cancellationToken);
                var functionName = GenerateFunctionName(endpoint);
                var groupName = endpoint.Group ?? "general";

                // Add imports
                var sb = new StringBuilder();
                var httpMethod = endpoint.HttpMethod.ToUpperInvariant();
                var isQuery = httpMethod == "GET";

                if (isQuery)
                {
                    sb.AppendLine("import { useQuery } from '@tanstack/react-query';");
                }
                else
                {
                    sb.AppendLine("import { useMutation } from '@tanstack/react-query';");
                }
                sb.AppendLine($"import {{ {functionName}Async }} from '../api/{ToCamelCase(groupName)}';");

                // Add type imports if needed
                if (endpoint.RequestType != null || endpoint.ResponseType != null)
                {
                    var typeNames = new List<string>();
                    if (endpoint.RequestType != null)
                        typeNames.Add(CleanTypeNameForImport(endpoint.RequestType.Name));
                    if (endpoint.ResponseType != null && endpoint.ResponseType.Name != "IResult")
                        typeNames.Add(CleanTypeNameForImport(endpoint.ResponseType.Name));

                    if (typeNames.Count > 0)
                        sb.AppendLine($"import type {{ {string.Join(", ", typeNames)} }} from '../types';");
                }

                sb.AppendLine();
                sb.Append(hookCode);

                var fileName = $"use{ToPascalCase(endpoint.ActionName)}.ts";
                await File.WriteAllTextAsync(Path.Combine(hooksDir, fileName), sb.ToString(), cancellationToken);
            }

            logger.LogInformation("   ✓ Generated {Count} React hooks", endpoints.Count);

            // Generate hooks/index.ts barrel export
            var hookExports = endpoints.Select(e => $"use{ToPascalCase(e.ActionName)}").ToList();
            var hooksIndexContent = await codeGenerator.GenerateBarrelExportAsync(hookExports, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(hooksDir, "index.ts"), hooksIndexContent, cancellationToken);
            logger.LogInformation("   ✓ Generated hooks/index.ts");
        }

        // Generate Next.js helpers (if enabled)
        if (config.Features.GenerateNextJsHelpers)
        {
            logger.LogInformation("   ▲ Generating Next.js server functions...");
            var serverDir = Path.Combine(outputPath, "server");
            Directory.CreateDirectory(serverDir);

            foreach (var endpoint in endpoints)
            {
                var serverCode = await codeGenerator.GenerateNextJsServerFunctionAsync(endpoint, cancellationToken);
                var fileName = $"{ToCamelCase(endpoint.ActionName)}.server.ts";
                await File.WriteAllTextAsync(Path.Combine(serverDir, fileName), serverCode, cancellationToken);
            }

            logger.LogInformation("   ✓ Generated {Count} server functions", endpoints.Count);
        }

        // Generate root index.ts to re-export from all subdirectories
        logger.LogInformation("   📋 Generating root index.ts...");
        var rootExports = new List<string> { "types", "api", "lib" };
        if (config.Features.GenerateReactHooks) rootExports.Add("hooks");
        if (hasValidationIndex) rootExports.Add("validation");

        var rootIndexContent = await codeGenerator.GenerateBarrelExportAsync(rootExports, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "index.ts"), rootIndexContent, cancellationToken);
        logger.LogInformation("   ✓ Generated root index.ts");

        // Phase 4: Package Generation
        logger.LogInformation("\n📦 Phase 4/4: Generating package files...");
        await GeneratePackageJsonAsync(outputPath, config, cancellationToken);
        await GenerateTsConfigAsync(outputPath, cancellationToken);
        await GenerateReadmeAsync(outputPath, config, cancellationToken);

        logger.LogInformation("   ✓ Generated package.json");
        logger.LogInformation("   ✓ Generated tsconfig.json");
        logger.LogInformation("   ✓ Generated README.md");

        stopwatch.Stop();
        logger.LogInformation("\n✅ SDK generated successfully in {Elapsed:0.00}s", stopwatch.Elapsed.TotalSeconds);
        logger.LogInformation("📁 Output: {OutputPath}\n", outputPath);
    }

    private static async Task WatchForChangesAsync(
        string configPath,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<GenerateCommand>();
        var configLoader = new ConfigurationLoader(loggerFactory.CreateLogger<ConfigurationLoader>());
        var config = await configLoader.LoadAsync(configPath, cancellationToken);

        if (config == null) return;

        var projectPath = config.ProjectPaths.Count > 0
            ? Path.GetFullPath(config.ProjectPaths[0])
            : Directory.GetCurrentDirectory();
        using var watcher = new FileSystemWatcher(projectPath)
        {
            Filter = "*.cs",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        var lastRun = DateTime.MinValue;
        var debounceDelay = TimeSpan.FromSeconds(2);

        watcher.Changed += async (sender, e) => await OnFileChanged();
        watcher.Created += async (sender, e) => await OnFileChanged();
        watcher.Deleted += async (sender, e) => await OnFileChanged();

        watcher.EnableRaisingEvents = true;

        async Task OnFileChanged()
        {
            var now = DateTime.UtcNow;
            if (now - lastRun < debounceDelay) return;

            lastRun = now;
            logger.LogInformation("🔄 Changes detected, regenerating SDK...");

            try
            {
                await RunGenerationAsync(config, loggerFactory, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error during regeneration");
            }
        }

        // Keep the watcher running
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private static async Task GeneratePackageJsonAsync(
        string outputPath,
        Core.Models.Configuration.CodeBridgeConfig config,
        CancellationToken cancellationToken)
    {
        var packageJson = $$"""
        {
          "name": "{{config.Output.PackageName}}",
          "version": "{{config.Output.PackageVersion}}",
          "description": "{{config.Output.Description ?? "Auto-generated TypeScript SDK"}}",
          "type": "module",
          "main": "./index.js",
          "types": "./index.d.ts",
          "license": "{{config.Output.License}}",
          {{(string.IsNullOrEmpty(config.Output.Author) ? "" : $$"""
          "author": "{{config.Output.Author}}",
          """)}}
          {{(string.IsNullOrEmpty(config.Output.Repository) ? "" : $$"""
          "repository": {
            "type": "git",
            "url": "{{config.Output.Repository}}"
          },
          """)}}
          "dependencies": {
            {{(config.Features.IncludeValidation ? "\"zod\": \"^3.22.0\"," : "")}}
            {{(config.Features.GenerateReactHooks ? "\"@tanstack/react-query\": \"^5.0.0\"," : "")}}
            "axios": "^1.6.0"
          },
          "devDependencies": {
            "typescript": "^5.9.0",
            "@types/node": "^22.0.0"
          }
        }
        """;

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "package.json"),
            packageJson,
            cancellationToken);
    }

    private static async Task GenerateTsConfigAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        var tsConfig = """
        {
          "compilerOptions": {
            "target": "ES2020",
            "module": "ESNext",
            "lib": ["ES2020", "DOM"],
            "moduleResolution": "bundler",
            "strict": true,
            "skipLibCheck": true,
            "esModuleInterop": true,
            "allowSyntheticDefaultImports": true,
            "declaration": true,
            "declarationMap": true,
            "sourceMap": true,
            "outDir": "./dist",
            "rootDir": ".",
            "types": ["node"]
          },
          "include": [
            "api/**/*",
            "hooks/**/*",
            "lib/**/*",
            "types/**/*",
            "validation/**/*",
            "index.ts"
          ],
          "exclude": ["node_modules", "dist"]
        }
        """;

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "tsconfig.json"),
            tsConfig,
            cancellationToken);
    }

    private static async Task GenerateReadmeAsync(
        string outputPath,
        Core.Models.Configuration.CodeBridgeConfig config,
        CancellationToken cancellationToken)
    {
        var codeBridgeVersion = GetCodeBridgeVersion();
        var features = new List<string>
        {
            "- ✅ Full TypeScript support",
            "- ✅ Auto-generated from C# API"
        };

        if (config.Features.IncludeValidation)
            features.Add("- ✅ Built-in validation with Zod");
        if (config.Features.GenerateReactHooks)
            features.Add("- ✅ React Query hooks");
        if (config.Features.GenerateNextJsHelpers)
            features.Add("- ✅ Next.js server actions");

        var readme = $@"# {config.Output.PackageName}

Auto-generated TypeScript SDK for your .NET API.

## Installation

```bash
npm install {config.Output.PackageName}
```

## Usage

```typescript
import {{ apiClient }} from '{config.Output.PackageName}';

// Configure the API client
apiClient.setBaseUrl('{config.Api.BaseUrl}');

// Make API calls
const result = await apiClient.users.getAll();
```

## Features

{string.Join("\n", features)}

## Generated with CodeBridge

This SDK was automatically generated using [CodeBridge](https://github.com/sodiqyekeen/CodeBridge) v{codeBridgeVersion}.

## License

{config.Output.License}
";

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "README.md"),
            readme,
            cancellationToken);
    }

    private static string GenerateFunctionName(EndpointInfo endpoint)
    {
        var httpMethod = endpoint.HttpMethod.ToLowerInvariant();
        var actionName = endpoint.ActionName;

        // Remove "Async" suffix if present
        if (actionName.EndsWith("Async"))
            actionName = actionName[..^5];

        // If action already contains the HTTP method, just use it
        if (actionName.ToLowerInvariant().StartsWith(httpMethod))
        {
            return ToCamelCase(actionName);
        }

        // Otherwise, combine method with action
        return ToCamelCase($"{httpMethod}{actionName}");
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

    private static string CleanTypeNameForImport(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;

        // Remove array suffix (User[] -> User, WeatherForecast[] -> WeatherForecast)
        typeName = typeName.TrimEnd('[', ']');

        // Extract all type names from generics: Result<User> -> "Result, User"
        // This is needed for import statements which must list all types separately
        var typeNames = new List<string>();
        ExtractAllTypeNames(typeName, typeNames);

        return string.Join(", ", typeNames.Distinct());
    }

    private static void ExtractAllTypeNames(string typeName, List<string> typeNames)
    {
        if (string.IsNullOrEmpty(typeName))
            return;

        var genericStart = typeName.IndexOf('<');
        if (genericStart > 0)
        {
            // Add base type: Result<User> -> add "Result"
            var baseType = typeName.Substring(0, genericStart).Trim();
            if (!string.IsNullOrEmpty(baseType))
                typeNames.Add(baseType);

            // Extract inner types: Result<User> -> "User"
            var genericEnd = typeName.LastIndexOf('>');
            if (genericEnd > genericStart)
            {
                var innerTypes = typeName.Substring(genericStart + 1, genericEnd - genericStart - 1);

                // Split by comma (accounting for nested generics)
                var parts = SplitGenericParts(innerTypes);
                foreach (var part in parts)
                {
                    ExtractAllTypeNames(part.Trim(), typeNames); // Recursive for nested generics
                }
            }
        }
        else
        {
            // Not a generic type, just add it
            var cleaned = typeName.Trim();
            if (!string.IsNullOrEmpty(cleaned))
                typeNames.Add(cleaned);
        }
    }

    private static List<string> SplitGenericParts(string genericArgs)
    {
        var parts = new List<string>();
        var currentPart = new StringBuilder();
        var depth = 0;

        foreach (var ch in genericArgs)
        {
            if (ch == '<')
            {
                depth++;
                currentPart.Append(ch);
            }
            else if (ch == '>')
            {
                depth--;
                currentPart.Append(ch);
            }
            else if (ch == ',' && depth == 0)
            {
                if (currentPart.Length > 0)
                {
                    parts.Add(currentPart.ToString().Trim());
                    currentPart.Clear();
                }
            }
            else
            {
                currentPart.Append(ch);
            }
        }

        if (currentPart.Length > 0)
        {
            parts.Add(currentPart.ToString().Trim());
        }

        return parts;
    }

    private static async Task<List<TypeInfo>> ExtractReferencedTypesFromEndpointsAsync(
        List<EndpointInfo> endpoints,
        List<TypeInfo> existingTypes,
        SourceAnalyzer sourceAnalyzer,
        List<Core.Models.ProjectInfo> projects,
        CancellationToken cancellationToken)
    {
        var referencedTypes = new List<TypeInfo>();
        var existingTypeNames = new HashSet<string>(existingTypes.Select(t => t.Name));
        var missingTypeNames = new HashSet<string>();

        // First pass: identify missing type names from both request AND response types
        foreach (var endpoint in endpoints)
        {
            // Extract from response type
            if (endpoint.ResponseType != null)
            {
                var responseTypeName = endpoint.ResponseType.Name;
                var responseBaseTypes = ExtractBaseTypeNames(responseTypeName);

                foreach (var baseType in responseBaseTypes)
                {
                    if (!existingTypeNames.Contains(baseType) &&
                        !IsPrimitiveOrFrameworkType(baseType))
                    {
                        missingTypeNames.Add(baseType);
                    }
                }
            }

            // Extract from request type
            if (endpoint.RequestType != null)
            {
                var requestTypeName = endpoint.RequestType.Name;
                var requestBaseTypes = ExtractBaseTypeNames(requestTypeName);

                foreach (var baseType in requestBaseTypes)
                {
                    if (!existingTypeNames.Contains(baseType) &&
                        !IsPrimitiveOrFrameworkType(baseType))
                    {
                        missingTypeNames.Add(baseType);
                    }
                }
            }

            // Extract from parameters
            if (endpoint.Parameters != null)
            {
                foreach (var param in endpoint.Parameters)
                {
                    if (param.Type != null)
                    {
                        var paramTypeName = param.Type.Name;
                        var paramBaseTypes = ExtractBaseTypeNames(paramTypeName);

                        foreach (var baseType in paramBaseTypes)
                        {
                            if (!existingTypeNames.Contains(baseType) &&
                                !IsPrimitiveOrFrameworkType(baseType))
                            {
                                missingTypeNames.Add(baseType);
                            }
                        }
                    }
                }
            }
        }

        // Second pass: re-discover all types and extract the missing ones
        // This ensures we get full property information
        if (missingTypeNames.Count > 0)
        {
            var allTypes = await sourceAnalyzer.DiscoverTypesAsync(projects, cancellationToken);

            foreach (var type in allTypes)
            {
                if (missingTypeNames.Contains(type.Name))
                {
                    referencedTypes.Add(type);
                    existingTypeNames.Add(type.Name); // Mark as found

                    // Recursively add property type dependencies
                    AddPropertyTypeDependencies(type, allTypes, referencedTypes, existingTypeNames, missingTypeNames);
                }
            }

            // Add placeholder for any types still not found
            foreach (var missingName in missingTypeNames)
            {
                if (!referencedTypes.Any(t => t.Name == missingName))
                {
                    referencedTypes.Add(new TypeInfo
                    {
                        Name = missingName,
                        FullName = missingName,
                        Namespace = null,
                        IsEnum = false,
                        Properties = new List<PropertyInfo>(),
                        IsGeneric = false,
                        GenericArguments = new List<TypeInfo>()
                    });
                }
            }
        }

        return referencedTypes;
    }

    private static void AddPropertyTypeDependencies(
        TypeInfo type,
        List<TypeInfo> allDiscoveredTypes,
        List<TypeInfo> referencedTypes,
        HashSet<string> existingTypeNames,
        HashSet<string> missingTypeNames)
    {
        // Examine all properties of this type
        foreach (var property in type.Properties)
        {
            var propertyTypeName = property.Type.Name;
            var baseTypes = ExtractBaseTypeNames(propertyTypeName);

            foreach (var baseType in baseTypes)
            {
                // Skip if already included or is a primitive
                if (existingTypeNames.Contains(baseType) || IsPrimitiveOrFrameworkType(baseType))
                    continue;

                // Find this type in the discovered types
                var dependencyType = allDiscoveredTypes.FirstOrDefault(t => t.Name == baseType);
                if (dependencyType != null)
                {
                    referencedTypes.Add(dependencyType);
                    existingTypeNames.Add(baseType);

                    // Recursively add its dependencies
                    AddPropertyTypeDependencies(dependencyType, allDiscoveredTypes, referencedTypes, existingTypeNames, missingTypeNames);
                }
                else
                {
                    // Track as missing for placeholder generation
                    missingTypeNames.Add(baseType);
                }
            }
        }
    }

    private static List<string> ExtractBaseTypeNames(string typeName)
    {
        var types = new List<string>();

        // Remove array suffix
        typeName = typeName.TrimEnd('[', ']');

        // Extract from generics: Result<User> -> [Result, User]
        if (typeName.Contains('<') && typeName.Contains('>'))
        {
            var match = System.Text.RegularExpressions.Regex.Match(typeName, @"^([^<]+)<(.+)>$");
            if (match.Success)
            {
                types.Add(match.Groups[1].Value.Trim()); // Generic type (Result)

                // Recursively extract inner types
                var innerTypes = ExtractBaseTypeNames(match.Groups[2].Value.Trim());
                types.AddRange(innerTypes);
            }
        }
        else
        {
            types.Add(typeName);
        }

        return types;
    }

    private static bool IsPrimitiveOrFrameworkType(string typeName)
    {
        var primitiveTypes = new HashSet<string>
        {
            "string", "int", "long", "decimal", "double", "float", "bool", "DateTime", "DateTimeOffset",
            "Guid", "byte", "short", "uint", "ulong", "ushort", "sbyte", "char", "object",
            "void", "any", "unknown", "never", "null", "undefined",
            "IResult", "Result", "ApiResponse", "IActionResult", "ActionResult"
        };

        return primitiveTypes.Contains(typeName);
    }

    private static string SanitizeFileName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;

        // Remove generic parameters: Result<User> -> Result, PagedResponse<T> -> PagedResponse
        var genericStart = typeName.IndexOf('<');
        if (genericStart > 0)
        {
            return typeName.Substring(0, genericStart);
        }

        // Remove array brackets: User[] -> User
        return typeName.TrimEnd('[', ']');
    }

    private static string GetCodeBridgeVersion()
    {
        var assembly = typeof(GenerateCommand).Assembly;
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }
}