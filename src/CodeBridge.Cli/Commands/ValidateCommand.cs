using System.CommandLine;
using CodeBridge.Core.Services;
using Microsoft.Extensions.Logging;

namespace CodeBridge.Cli.Commands;

/// <summary>
/// Command to validate CodeBridge configuration file.
/// </summary>
public sealed class ValidateCommand : Command
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateCommand"/> class.
    /// Configures option for configuration file path validation.
    /// </summary>
    public ValidateCommand() : base("validate", "Validate CodeBridge configuration file")
    {
        var configOption = new Option<string>(
            aliases: new[] { "--config", "-c" },
            description: "Path to codebridge.json configuration file",
            getDefaultValue: () => "./codebridge.json");

        AddOption(configOption);

        this.SetHandler(async (context) =>
        {
            var configPath = context.ParseResult.GetValueForOption(configOption)!;
            await ExecuteAsync(configPath, context.GetCancellationToken());
        });
    }

    private static async Task ExecuteAsync(string configPath, CancellationToken cancellationToken)
    {
        Console.WriteLine($"🔍 Validating configuration: {configPath}\n");

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        var logger = loggerFactory.CreateLogger<ValidateCommand>();

        try
        {
            // Check if file exists
            if (!File.Exists(configPath))
            {
                logger.LogError("❌ Configuration file not found: {ConfigPath}", configPath);
                return;
            }

            // Load and validate configuration
            var configLoader = new ConfigurationLoader(loggerFactory.CreateLogger<ConfigurationLoader>());
            var config = await configLoader.LoadAsync(configPath, cancellationToken);

            if (config == null)
            {
                logger.LogError("❌ Failed to load configuration file");
                return;
            }

            // Perform validation checks
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate solution or project paths
            if (!string.IsNullOrEmpty(config.SolutionPath) && !File.Exists(config.SolutionPath))
            {
                errors.Add($"Solution file does not exist: {config.SolutionPath}");
            }

            foreach (var projectPath in config.ProjectPaths)
            {
                if (!Directory.Exists(projectPath))
                {
                    errors.Add($"Project path does not exist: {projectPath}");
                }
            }

            // Validate output path
            var outputPath = Path.GetFullPath(config.Output.Path);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null && !Directory.Exists(outputDir))
            {
                warnings.Add($"Output directory parent does not exist: {outputDir}");
            }

            // Validate API URL
            if (!Uri.TryCreate(config.Api.BaseUrl, UriKind.Absolute, out var apiUri))
            {
                errors.Add($"Invalid API base URL: {config.Api.BaseUrl}");
            }

            // Validate package name
            if (string.IsNullOrWhiteSpace(config.Output.PackageName))
            {
                errors.Add("Package name is required");
            }
            else if (!IsValidPackageName(config.Output.PackageName))
            {
                warnings.Add($"Package name may not be valid npm package name: {config.Output.PackageName}");
            }

            // Validate version
            if (!IsValidSemVer(config.Output.PackageVersion))
            {
                errors.Add($"Invalid semantic version: {config.Output.PackageVersion}");
            }

            // Validate feature combinations
            if (config.Features.GenerateReactHooks && config.Target.Framework != Core.Models.Configuration.TargetFramework.React)
            {
                warnings.Add("GenerateReactHooks is enabled but target framework is not React");
            }

            if (config.Features.GenerateNextJsHelpers && config.Target.Framework != Core.Models.Configuration.TargetFramework.NextJs)
            {
                warnings.Add("GenerateNextJsHelpers is enabled but target framework is not Next.js");
            }

            // Display results
            if (errors.Count > 0)
            {
                Console.WriteLine("❌ Validation failed with errors:\n");
                foreach (var error in errors)
                {
                    logger.LogError("   • {Error}", error);
                }
                Console.WriteLine();
            }

            if (warnings.Count > 0)
            {
                Console.WriteLine("⚠️  Warnings:\n");
                foreach (var warning in warnings)
                {
                    logger.LogWarning("   • {Warning}", warning);
                }
                Console.WriteLine();
            }

            if (errors.Count == 0)
            {
                Console.WriteLine("✅ Configuration is valid!\n");

                // Display summary
                Console.WriteLine("📋 Configuration Summary:");
                Console.WriteLine($"   Solution: {config.SolutionPath ?? "N/A"}");
                Console.WriteLine($"   Projects: {string.Join(", ", config.ProjectPaths)}");
                Console.WriteLine($"   Output: {config.Output.Path}");
                Console.WriteLine($"   Target: {config.Target.Framework}");
                Console.WriteLine($"   Package: {config.Output.PackageName}@{config.Output.PackageVersion}");
                Console.WriteLine($"   Features:");
                Console.WriteLine($"      • Validation: {(config.Features.IncludeValidation ? "✓" : "✗")}");
                Console.WriteLine($"      • React Hooks: {(config.Features.GenerateReactHooks ? "✓" : "✗")}");
                Console.WriteLine($"      • Next.js Helpers: {(config.Features.GenerateNextJsHelpers ? "✓" : "✗")}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error validating configuration");
        }
    }

    private static bool IsValidPackageName(string packageName)
    {
        // Basic npm package name validation
        if (packageName.StartsWith('@'))
        {
            // Scoped package: @scope/name
            var parts = packageName.Split('/');
            if (parts.Length != 2) return false;
            return IsValidNamePart(parts[0][1..]) && IsValidNamePart(parts[1]);
        }

        return IsValidNamePart(packageName);
    }

    private static bool IsValidNamePart(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Length > 214) return false;
        if (name.StartsWith('.') || name.StartsWith('_')) return false;

        return name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
    }

    private static bool IsValidSemVer(string version)
    {
        // Basic semantic version validation (major.minor.patch)
        if (string.IsNullOrEmpty(version)) return false;

        var parts = version.Split('.');
        if (parts.Length != 3) return false;

        return parts.All(p => int.TryParse(p, out _));
    }
}
