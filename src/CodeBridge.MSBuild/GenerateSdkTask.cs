using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using CodeBridge.Core.Services;
using CodeBridge.Core.Models;
using CodeBridge.Core.Models.Configuration;
using System.Diagnostics;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace CodeBridge.MSBuild;

/// <summary>
/// MSBuild task for generating TypeScript SDK during build.
/// </summary>
public sealed class GenerateSdkTask : MSBuildTask, ICancelableTask
{
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Path to codebridge.json configuration file.
    /// </summary>
    [Required]
    public string? ConfigurationFile { get; set; }

    /// <summary>
    /// Project file path.
    /// </summary>
    public string? ProjectFile { get; set; }

    /// <summary>
    /// Build configuration (Debug, Release, etc.).
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// Enable verbose logging.
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Skip generation if no changes detected.
    /// </summary>
    public bool Incremental { get; set; } = true;

    /// <summary>
    /// Executes the SDK generation task synchronously.
    /// </summary>
    /// <returns>True if generation succeeded; otherwise, false.</returns>
    /// <remarks>
    /// This method wraps the asynchronous ExecuteAsync method for MSBuild compatibility.
    /// Generation can be cancelled using the Cancel() method.
    /// </remarks>
    public override bool Execute()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            return ExecuteAsync(_cancellationTokenSource.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Log.LogWarning("SDK generation was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: Verbose);
            return false;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
        }
    }

    private async System.Threading.Tasks.Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        Log.LogMessage(MessageImportance.High, "CodeBridge: Starting SDK generation...");

        if (string.IsNullOrEmpty(ConfigurationFile))
        {
            Log.LogError("ConfigurationFile is required");
            return false;
        }

        if (!File.Exists(ConfigurationFile))
        {
            Log.LogError($"Configuration file not found: {ConfigurationFile}");
            return false;
        }

        // Create logger adapter
        var logger = new MSBuildLoggerAdapter(Log, Verbose);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new MSBuildLoggerProvider(Log, Verbose));
            builder.SetMinimumLevel(Verbose ? LogLevel.Debug : LogLevel.Information);
        });

        try
        {
            // Load configuration
            var configLoader = new ConfigurationLoader(loggerFactory.CreateLogger<ConfigurationLoader>());
            var config = await configLoader.LoadAsync(ConfigurationFile, cancellationToken);

            if (config == null)
            {
                Log.LogError("Failed to load configuration");
                return false;
            }

            // Validate configuration
            var errors = config.Validate();
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Log.LogError(error);
                }
                return false;
            }

            // Check if generation should be skipped
            if (Incremental && ShouldSkipGeneration(config))
            {
                Log.LogMessage(MessageImportance.Normal, "CodeBridge: No changes detected, skipping generation");
                return true;
            }

            // Run generation pipeline
            var success = await RunGenerationPipelineAsync(config, loggerFactory, cancellationToken);

            stopwatch.Stop();

            if (success)
            {
                Log.LogMessage(MessageImportance.High,
                    $"CodeBridge: SDK generated successfully in {stopwatch.Elapsed.TotalSeconds:0.00}s");
            }
            else
            {
                Log.LogError("CodeBridge: SDK generation failed");
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: Verbose);
            return false;
        }
    }

    private async System.Threading.Tasks.Task<bool> RunGenerationPipelineAsync(
        CodeBridgeConfig config,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<GenerateSdkTask>();

        try
        {
            // Phase 1: Source Analysis
            logger.LogInformation("Phase 1/4: Analyzing source code...");
            var sourceAnalyzer = new SourceAnalyzer(loggerFactory.CreateLogger<SourceAnalyzer>());

            // Build project list
            var projects = new List<ProjectInfo>();
            if (!string.IsNullOrEmpty(config.SolutionPath))
            {
                logger.LogWarning("Solution file parsing not yet implemented, using direct project paths");
            }

            if (config.ProjectPaths.Count == 0)
            {
                // Use project file directory if available
                var projectDir = !string.IsNullOrEmpty(ProjectFile)
                    ? Path.GetDirectoryName(ProjectFile)!
                    : Directory.GetCurrentDirectory();
                config.ProjectPaths.Add(projectDir);
            }

            foreach (var projectPath in config.ProjectPaths)
            {
                var fullPath = Path.GetFullPath(projectPath);
                projects.Add(new ProjectInfo
                {
                    Name = Path.GetFileName(fullPath),
                    Path = fullPath
                });
            }

            var endpoints = await sourceAnalyzer.DiscoverEndpointsAsync(projects, cancellationToken);
            var types = await sourceAnalyzer.DiscoverTypesAsync(projects, cancellationToken);

            logger.LogInformation("Found {EndpointCount} endpoints and {TypeCount} types",
                endpoints.Count, types.Count);

            if (endpoints.Count == 0)
            {
                logger.LogWarning("No endpoints with [GenerateSdk] attribute found");
                return true; // Not an error, just nothing to generate
            }

            // Phase 2: Validation Analysis
            Dictionary<string, List<PropertyValidationRules>>? validationRules = null;
            if (config.Features.IncludeValidation)
            {
                logger.LogInformation("Phase 2/4: Analyzing validation rules...");
                validationRules = await sourceAnalyzer.DiscoverValidationRulesAsync(projects, cancellationToken);
                logger.LogInformation("Found validation rules for {Count} types", validationRules.Count);
            }
            else
            {
                logger.LogInformation("Phase 2/4: Skipped (validation disabled)");
            }

            // Phase 3: Code Generation
            logger.LogInformation("Phase 3/4: Generating TypeScript code...");

            var outputPath = Path.GetFullPath(config.Output.Path);
            if (config.Output.CleanBeforeGenerate && Directory.Exists(outputPath))
            {
                logger.LogDebug("Cleaning output directory...");
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

            foreach (var type in types)
            {
                var content = type.IsEnum
                    ? await codeGenerator.GenerateTypeScriptEnumAsync(type, cancellationToken)
                    : await codeGenerator.GenerateTypeScriptInterfaceAsync(type, cancellationToken);

                var fileName = $"{ToCamelCase(type.Name)}.ts";
                await File.WriteAllTextAsync(Path.Combine(typesDir, fileName), content, cancellationToken);
            }

            logger.LogInformation("Generated {Count} type files", types.Count);

            // Generate API client
            var apiDir = Path.Combine(outputPath, "api");
            Directory.CreateDirectory(apiDir);

            var groupedEndpoints = endpoints.GroupBy(e => e.Group ?? "general");
            foreach (var group in groupedEndpoints)
            {
                var functions = new List<string>();
                foreach (var endpoint in group)
                {
                    var functionCode = await codeGenerator.GenerateApiClientFunctionAsync(
                        endpoint,
                        config.Features.IncludeValidation,
                        cancellationToken);
                    functions.Add(functionCode);
                }

                var groupFileName = $"{ToCamelCase(group.Key)}.ts";
                var groupContent = string.Join("\n\n", functions);
                await File.WriteAllTextAsync(Path.Combine(apiDir, groupFileName), groupContent, cancellationToken);
            }

            logger.LogInformation("Generated {Count} API client files", groupedEndpoints.Count());

            // Generate validation schemas
            if (config.Features.IncludeValidation && validationRules != null)
            {
                var validationDir = Path.Combine(outputPath, "validation");
                Directory.CreateDirectory(validationDir);

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
                    }
                }

                logger.LogInformation("Generated validation schemas");
            }

            // Generate React hooks
            if (config.Features.GenerateReactHooks)
            {
                logger.LogInformation("Generating React hooks...");
                var hooksDir = Path.Combine(outputPath, "hooks");
                Directory.CreateDirectory(hooksDir);

                foreach (var endpoint in endpoints)
                {
                    var hookCode = await codeGenerator.GenerateReactHookAsync(endpoint, cancellationToken);
                    var fileName = $"use{ToPascalCase(endpoint.ActionName)}.ts";
                    await File.WriteAllTextAsync(Path.Combine(hooksDir, fileName), hookCode, cancellationToken);
                }

                logger.LogInformation("Generated {Count} React hooks", endpoints.Count);
            }

            // Generate Next.js helpers
            if (config.Features.GenerateNextJsHelpers)
            {
                logger.LogInformation("Generating Next.js server functions...");
                var serverDir = Path.Combine(outputPath, "server");
                Directory.CreateDirectory(serverDir);

                foreach (var endpoint in endpoints)
                {
                    var serverCode = await codeGenerator.GenerateNextJsServerFunctionAsync(endpoint, cancellationToken);
                    var fileName = $"{ToCamelCase(endpoint.ActionName)}.server.ts";
                    await File.WriteAllTextAsync(Path.Combine(serverDir, fileName), serverCode, cancellationToken);
                }

                logger.LogInformation("Generated {Count} server functions", endpoints.Count);
            }

            // Phase 4: Package files
            logger.LogInformation("Phase 4/4: Generating package files...");
            await GeneratePackageFilesAsync(outputPath, config, cancellationToken);
            logger.LogInformation("Generated package.json and README.md");

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during SDK generation");
            return false;
        }
    }

    private bool ShouldSkipGeneration(CodeBridgeConfig config)
    {
        // Check if output directory exists and has content
        var outputPath = Path.GetFullPath(config.Output.Path);
        if (!Directory.Exists(outputPath))
            return false;

        var hasFiles = Directory.GetFiles(outputPath, "*.ts", SearchOption.AllDirectories).Any();
        if (!hasFiles)
            return false;

        // Check last write time of source files vs generated files
        var sourceFiles = new List<string>();
        foreach (var projectPath in config.ProjectPaths)
        {
            var fullPath = Path.GetFullPath(projectPath);
            if (Directory.Exists(fullPath))
            {
                sourceFiles.AddRange(Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/")));
            }
        }

        if (sourceFiles.Count == 0)
            return false;

        var lastSourceWrite = sourceFiles.Max(f => File.GetLastWriteTimeUtc(f));
        var generatedFiles = Directory.GetFiles(outputPath, "*.ts", SearchOption.AllDirectories);
        var lastGeneratedWrite = generatedFiles.Max(f => File.GetLastWriteTimeUtc(f));

        // Skip if generated files are newer than source files
        return lastGeneratedWrite > lastSourceWrite;
    }

    private static async System.Threading.Tasks.Task GeneratePackageFilesAsync(
        string outputPath,
        CodeBridgeConfig config,
        CancellationToken cancellationToken)
    {
        // Generate package.json
        var dependencies = new List<string>();
        if (config.Features.IncludeValidation)
            dependencies.Add("\"zod\": \"^3.22.0\"");
        if (config.Features.GenerateReactHooks)
            dependencies.Add("\"@tanstack/react-query\": \"^5.0.0\"");
        dependencies.Add("\"axios\": \"^1.6.0\"");

        var packageJson = $$"""
        {
          "name": "{{config.Output.PackageName}}",
          "version": "{{config.Output.PackageVersion}}",
          "description": "{{config.Output.Description ?? "Auto-generated TypeScript SDK"}}",
          "type": "module",
          "main": "./index.js",
          "types": "./index.d.ts",
          "license": "{{config.Output.License}}",
          {{(!string.IsNullOrEmpty(config.Output.Author) ? $$"""
          "author": "{{config.Output.Author}}",
          """ : "")}}
          {{(!string.IsNullOrEmpty(config.Output.Repository) ? $$"""
          "repository": {
            "type": "git",
            "url": "{{config.Output.Repository}}"
          },
          """ : "")}}
          "dependencies": {
            {{string.Join(",\n    ", dependencies)}}
          }
        }
        """;

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "package.json"),
            packageJson,
            cancellationToken);

        // Generate README.md
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

This SDK was automatically generated using [CodeBridge](https://github.com/clywell/codebridge).
";

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "README.md"),
            readme,
            cancellationToken);
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

    /// <summary>
    /// Cancels the ongoing SDK generation task.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="ICancelableTask"/> to support build cancellation in MSBuild.
    /// </remarks>
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }
}

/// <summary>
/// Logger adapter for MSBuild task logging.
/// </summary>
internal sealed class MSBuildLoggerAdapter : ILogger
{
    private readonly TaskLoggingHelper _log;
    private readonly bool _verbose;

    public MSBuildLoggerAdapter(TaskLoggingHelper log, bool verbose)
    {
        _log = log;
        _verbose = verbose;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
    {
        return _verbose || logLevel >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);

        switch (logLevel)
        {
            case LogLevel.Critical:
            case LogLevel.Error:
                _log.LogError(message);
                break;
            case LogLevel.Warning:
                _log.LogWarning(message);
                break;
            case LogLevel.Information:
                _log.LogMessage(MessageImportance.Normal, message);
                break;
            case LogLevel.Debug:
            case LogLevel.Trace:
                if (_verbose)
                    _log.LogMessage(MessageImportance.Low, message);
                break;
        }
    }
}

/// <summary>
/// Logger provider for MSBuild task logging.
/// </summary>
internal sealed class MSBuildLoggerProvider : ILoggerProvider
{
    private readonly TaskLoggingHelper _log;
    private readonly bool _verbose;

    public MSBuildLoggerProvider(TaskLoggingHelper log, bool verbose)
    {
        _log = log;
        _verbose = verbose;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new MSBuildLoggerAdapter(_log, _verbose);
    }

    public void Dispose()
    {
    }
}
