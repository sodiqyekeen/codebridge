using System.Text.Json;
using CodeBridge.Core.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeBridge.Core.Services;

/// <summary>
/// Loads and validates CodeBridge configuration from JSON files.
/// Supports environment-specific overrides (e.g., codebridge.Development.json).
/// </summary>
public sealed class ConfigurationLoader(ILogger<ConfigurationLoader> logger) : IConfigurationLoader
{
    private readonly ILogger<ConfigurationLoader> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    /// <summary>
    /// Loads configuration from the specified path or searches for codebridge.json in the current directory.
    /// Applies environment-specific overrides if available.
    /// </summary>
    public async Task<CodeBridgeConfig> LoadAsync(string? configPath = null, CancellationToken cancellationToken = default)
    {
        configPath ??= FindConfigurationFile();

        if (string.IsNullOrEmpty(configPath))
        {
            throw new FileNotFoundException(
                "Configuration file not found. Please create a codebridge.json file or specify the path explicitly. " +
                "Run 'codebridge init' to create a default configuration.");
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        _logger.LogInformation("Loading configuration from: {ConfigPath}", configPath);

        // Load base configuration
        var config = await LoadConfigFileAsync(configPath, cancellationToken);

        // Apply environment-specific overrides
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                         ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (!string.IsNullOrEmpty(environment))
        {
            var environmentConfigPath = GetEnvironmentConfigPath(configPath, environment);
            if (File.Exists(environmentConfigPath))
            {
                _logger.LogInformation("Applying environment overrides from: {EnvironmentConfigPath}", environmentConfigPath);
                var environmentConfig = await LoadConfigFileAsync(environmentConfigPath, cancellationToken);
                config = MergeConfigurations(config, environmentConfig);
            }
        }

        // Validate the merged configuration
        Validate(config);

        return config;
    }

    /// <summary>
    /// Validates the configuration and throws an exception if any issues are found.
    /// </summary>
    public void Validate(CodeBridgeConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var validationErrors = config.Validate();
        if (validationErrors.Count > 0)
        {
            var errorMessage = "Configuration validation failed:" + Environment.NewLine +
                             string.Join(Environment.NewLine, validationErrors.Select(e => $"  - {e}"));

            _logger.LogError("Configuration validation failed: {Errors}", validationErrors);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogDebug("Configuration validation passed");
    }

    /// <summary>
    /// Searches for codebridge.json in the current directory and parent directories.
    /// </summary>
    private static string? FindConfigurationFile()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var searchDirectory = currentDirectory;

        while (!string.IsNullOrEmpty(searchDirectory))
        {
            var configPath = Path.Combine(searchDirectory, "codebridge.json");
            if (File.Exists(configPath))
            {
                return configPath;
            }

            // Also check for .codebridge.json (hidden file)
            var hiddenConfigPath = Path.Combine(searchDirectory, ".codebridge.json");
            if (File.Exists(hiddenConfigPath))
            {
                return hiddenConfigPath;
            }

            var parentDirectory = Directory.GetParent(searchDirectory)?.FullName;
            if (parentDirectory == searchDirectory)
            {
                break; // Reached the root
            }

            searchDirectory = parentDirectory;
        }

        return null;
    }

    /// <summary>
    /// Loads a configuration file from the specified path.
    /// </summary>
    private async Task<CodeBridgeConfig> LoadConfigFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var config = JsonSerializer.Deserialize<CodeBridgeConfig>(json, JsonOptions);

            if (config == null)
            {
                throw new InvalidOperationException($"Failed to deserialize configuration from: {path}");
            }

            return config;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse configuration file: {Path}", path);
            throw new InvalidOperationException($"Invalid JSON in configuration file: {path}. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the path for an environment-specific configuration file.
    /// Example: codebridge.json -> codebridge.Development.json
    /// </summary>
    private static string GetEnvironmentConfigPath(string basePath, string environment)
    {
        var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        return Path.Combine(directory, $"{fileName}.{environment}{extension}");
    }

    /// <summary>
    /// Merges environment-specific configuration into the base configuration.
    /// Environment config takes precedence over base config.
    /// </summary>
    private CodeBridgeConfig MergeConfigurations(CodeBridgeConfig baseConfig, CodeBridgeConfig environmentConfig)
    {
        // Create a new config with environment overrides
        return new CodeBridgeConfig
        {
            SolutionPath = environmentConfig.SolutionPath ?? baseConfig.SolutionPath,
            ProjectPaths = environmentConfig.ProjectPaths?.Count > 0
                ? environmentConfig.ProjectPaths
                : baseConfig.ProjectPaths,

            Generation = MergeGenerationOptions(baseConfig.Generation, environmentConfig.Generation),
            Output = MergeOutputOptions(baseConfig.Output, environmentConfig.Output),
            Target = MergeTargetOptions(baseConfig.Target, environmentConfig.Target),
            Api = MergeApiOptions(baseConfig.Api, environmentConfig.Api),
            Features = MergeFeatureOptions(baseConfig.Features, environmentConfig.Features),
            Discovery = MergeDiscoveryOptions(baseConfig.Discovery, environmentConfig.Discovery),
            Advanced = MergeAdvancedOptions(baseConfig.Advanced, environmentConfig.Advanced)
        };
    }

    private static GenerationOptions MergeGenerationOptions(GenerationOptions? base_, GenerationOptions? env)
    {
        if (env == null) return base_ ?? new GenerationOptions();
        if (base_ == null) return env;

        return new GenerationOptions
        {
            Mode = env.Mode,
            BuildEvents = env.BuildEvents?.Count > 0 ? env.BuildEvents : base_.BuildEvents,
            WatchPaths = env.WatchPaths?.Count > 0 ? env.WatchPaths : base_.WatchPaths,
            DebounceMs = env.DebounceMs,
            ValidateOnly = env.ValidateOnly
        };
    }

    private static OutputOptions MergeOutputOptions(OutputOptions? base_, OutputOptions? env)
    {
        if (env == null) return base_ ?? new OutputOptions();
        if (base_ == null) return env;

        return new OutputOptions
        {
            Path = env.Path ?? base_.Path,
            PackageName = env.PackageName ?? base_.PackageName,
            PackageVersion = env.PackageVersion ?? base_.PackageVersion,
            Author = env.Author ?? base_.Author,
            Description = env.Description ?? base_.Description,
            License = env.License ?? base_.License,
            Repository = env.Repository ?? base_.Repository,
            CleanBeforeGenerate = env.CleanBeforeGenerate
        };
    }

    private static TargetOptions MergeTargetOptions(TargetOptions? base_, TargetOptions? env)
    {
        if (env == null) return base_ ?? new TargetOptions();
        if (base_ == null) return env;

        return new TargetOptions
        {
            Framework = env.Framework,
            Language = env.Language,
            ModuleSystem = env.ModuleSystem,
            TypeScriptTarget = env.TypeScriptTarget ?? base_.TypeScriptTarget
        };
    }

    private static ApiOptions MergeApiOptions(ApiOptions? base_, ApiOptions? env)
    {
        if (env == null) return base_ ?? new ApiOptions();
        if (base_ == null) return env;

        return new ApiOptions
        {
            BaseUrl = env.BaseUrl ?? base_.BaseUrl,
            Timeout = env.Timeout,
            Authentication = env.Authentication ?? base_.Authentication,
            Csrf = env.Csrf ?? base_.Csrf
        };
    }

    private static FeatureOptions MergeFeatureOptions(FeatureOptions? base_, FeatureOptions? env)
    {
        if (env == null) return base_ ?? new FeatureOptions();
        if (base_ == null) return env;

        return new FeatureOptions
        {
            IncludeValidation = env.IncludeValidation,
            IncludeAuthentication = env.IncludeAuthentication,
            GenerateReactHooks = env.GenerateReactHooks,
            GenerateNextJsHelpers = env.GenerateNextJsHelpers
        };
    }

    private static DiscoveryOptions MergeDiscoveryOptions(DiscoveryOptions? base_, DiscoveryOptions? env)
    {
        if (env == null) return base_ ?? new DiscoveryOptions();
        if (base_ == null) return env;

        return new DiscoveryOptions
        {
            AutoDiscover = env.AutoDiscover,
            ProjectNamePatterns = env.ProjectNamePatterns?.Count > 0
                ? env.ProjectNamePatterns
                : base_.ProjectNamePatterns,
            NamespacePatterns = env.NamespacePatterns?.Count > 0
                ? env.NamespacePatterns
                : base_.NamespacePatterns
        };
    }

    private static AdvancedOptions MergeAdvancedOptions(AdvancedOptions? base_, AdvancedOptions? env)
    {
        if (env == null) return base_ ?? new AdvancedOptions();
        if (base_ == null) return env;

        return new AdvancedOptions
        {
            CustomTypeMappings = env.CustomTypeMappings?.Count > 0
                ? env.CustomTypeMappings
                : base_.CustomTypeMappings,
            ExcludedEndpoints = env.ExcludedEndpoints?.Count > 0
                ? env.ExcludedEndpoints
                : base_.ExcludedEndpoints,
            ExcludedTypes = env.ExcludedTypes?.Count > 0
                ? env.ExcludedTypes
                : base_.ExcludedTypes
        };
    }
}
