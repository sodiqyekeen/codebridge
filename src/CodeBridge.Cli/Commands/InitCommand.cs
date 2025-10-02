using System.CommandLine;
using System.CommandLine.Invocation;
using CodeBridge.Core.Models.Configuration;
using CodeBridge.Core.Services;
using Microsoft.Extensions.Logging;

namespace CodeBridge.Cli.Commands;

/// <summary>
/// Command to initialize a new CodeBridge configuration.
/// </summary>
public sealed class InitCommand : Command
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InitCommand"/> class.
    /// Configures options for template selection, output directory, API URL, and framework selection.
    /// </summary>
    public InitCommand() : base("init", "Initialize a new CodeBridge configuration file")
    {
        var templateOption = new Option<string>(
            aliases: new[] { "--template", "-t" },
            description: "Template to use (react, nextjs, vue, angular, vanilla)")
        {
            IsRequired = false
        };

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output directory for generated SDK",
            getDefaultValue: () => "./generated-sdk");

        var apiUrlOption = new Option<string>(
            aliases: new[] { "--api-url", "-u" },
            description: "Base URL of the API",
            getDefaultValue: () => "https://localhost:5001");

        var interactiveOption = new Option<bool>(
            aliases: new[] { "--interactive", "-i" },
            description: "Run in interactive mode",
            getDefaultValue: () => true);

        AddOption(templateOption);
        AddOption(outputOption);
        AddOption(apiUrlOption);
        AddOption(interactiveOption);

        this.SetHandler(async (context) =>
        {
            var template = context.ParseResult.GetValueForOption(templateOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var apiUrl = context.ParseResult.GetValueForOption(apiUrlOption)!;
            var interactive = context.ParseResult.GetValueForOption(interactiveOption);

            await ExecuteAsync(template, output, apiUrl, interactive, context.GetCancellationToken());
        });
    }

    private static async Task ExecuteAsync(
        string? template,
        string output,
        string apiUrl,
        bool interactive,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("🚀 Initializing CodeBridge configuration...\n");

        // If interactive mode, prompt for values
        if (interactive)
        {
            template = PromptForTemplate(template);
            output = PromptForValue("Output directory", output);
            apiUrl = PromptForValue("API base URL", apiUrl);
        }
        else if (string.IsNullOrEmpty(template))
        {
            template = "react"; // Default template
        }

        // Create configuration
        var config = CreateConfiguration(template!, output, apiUrl);

        // Generate configuration file
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var configLoader = new ConfigurationLoader(loggerFactory.CreateLogger<ConfigurationLoader>());
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "codebridge.json");

        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json, cancellationToken);

        Console.WriteLine($"\n✅ Configuration file created: {configPath}");
        Console.WriteLine("\n📝 Next steps:");
        Console.WriteLine("   1. Review and customize codebridge.json");
        Console.WriteLine("   2. Add [GenerateSdk] attributes to your API endpoints");
        Console.WriteLine("   3. Run 'codebridge generate' to generate the SDK\n");
    }

    private static string PromptForTemplate(string? defaultValue)
    {
        Console.WriteLine("Select a template:");
        Console.WriteLine("  1. React (with React Query)");
        Console.WriteLine("  2. Next.js (App Router)");
        Console.WriteLine("  3. Vue (with Pinia)");
        Console.WriteLine("  4. Angular");
        Console.WriteLine("  5. Vanilla TypeScript");
        Console.Write($"\nEnter choice (1-5) [{defaultValue ?? "1"}]: ");

        var choice = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(choice))
        {
            return defaultValue ?? "react";
        }

        return choice.Trim() switch
        {
            "1" => "react",
            "2" => "nextjs",
            "3" => "vue",
            "4" => "angular",
            "5" => "vanilla",
            _ => defaultValue ?? "react"
        };
    }

    private static string PromptForValue(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        var value = Console.ReadLine();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static CodeBridgeConfig CreateConfiguration(string template, string output, string apiUrl)
    {
        var targetFramework = template.ToLowerInvariant() switch
        {
            "react" => TargetFramework.React,
            "nextjs" => TargetFramework.NextJs,
            "vue" => TargetFramework.Vue,
            "angular" => TargetFramework.Angular,
            "vanilla" => TargetFramework.Vanilla,
            _ => TargetFramework.React
        };

        return new CodeBridgeConfig
        {
            SolutionPath = null,
            ProjectPaths = new List<string>(),
            Target = new TargetOptions
            {
                Framework = targetFramework,
                Language = "typescript",
                ModuleSystem = ModuleSystem.ESM
            },
            Api = new ApiOptions
            {
                BaseUrl = apiUrl,
                Authentication = new AuthenticationOptions
                {
                    Type = AuthenticationType.Bearer
                }
            },
            Output = new OutputOptions
            {
                Path = output,
                PackageName = "@myorg/api-client",
                PackageVersion = "1.0.0",
                License = "MIT",
                CleanBeforeGenerate = true
            },
            Features = new FeatureOptions
            {
                IncludeValidation = true,
                GenerateReactHooks = targetFramework == TargetFramework.React,
                GenerateNextJsHelpers = targetFramework == TargetFramework.NextJs
            },
            Generation = new GenerationOptions
            {
                Mode = GenerationMode.Manual
            }
        };
    }
}
