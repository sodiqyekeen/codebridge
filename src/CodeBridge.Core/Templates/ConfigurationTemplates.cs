namespace CodeBridge.Core.Templates;

/// <summary>
/// Provides JSON configuration templates for CodeBridge initialization.
/// </summary>
public static class ConfigurationTemplates
{
    /// <summary>
    /// Minimal configuration template for quick start.
    /// </summary>
    public const string MinimalTemplate = """
{
  "$schema": "https://codebridge.dev/schema.json",
  "solutionPath": "./YourSolution.sln",
  "output": {
    "path": "./sdk",
    "packageName": "your-api-client",
    "version": "1.0.0"
  },
  "api": {
    "baseUrl": "https://localhost:5001"
  }
}
""";

    /// <summary>
    /// Full configuration template with all available options and comments.
    /// </summary>
    public const string FullTemplate = """
{
  "$schema": "https://codebridge.dev/schema.json",
  
  // Solution and project configuration
  "solutionPath": "./YourSolution.sln",
  "projectPaths": [
    // Optional: Specify individual projects instead of solution
    // "./src/YourApi/YourApi.csproj"
  ],
  
  // Generation mode and behavior
  "generation": {
    "mode": "Manual",                    // Manual, BuildIntegration, Watch
    "buildEvents": ["AfterBuild"],       // BeforeBuild, AfterBuild, AfterPublish
    "watchPaths": ["./src/**/*.cs"],     // Paths to watch in Watch mode
    "debounceMs": 500,                   // Debounce delay for file changes
    "validateOnly": false                // Only validate without generating
  },
  
  // Output configuration
  "output": {
    "path": "./sdk",
    "packageName": "your-api-client",
    "packageVersion": "1.0.0",
    "author": "Your Name",
    "license": "MIT",
    "cleanBeforeGenerate": true
  },
  
  // Target framework and language
  "target": {
    "framework": "React",                // React, NextJs, Vue, Angular, Vanilla
    "language": "TypeScript",            // TypeScript, JavaScript
    "moduleSystem": "ESM",               // ESM, CommonJS
    "typeScriptTarget": "ES2020"         // ES2015, ES2016, ES2017, ES2018, ES2019, ES2020, ES2021, ES2022, ESNext
  },
  
  // API client configuration
  "api": {
    "baseUrl": "https://localhost:5001",
    "timeout": 30000,                    // Request timeout in milliseconds
    "authentication": {
      "type": "Bearer",                  // Bearer, ApiKey, None
      "storage": "LocalStorage",         // LocalStorage, SessionStorage, Memory, Cookie
      "tokenKey": "auth_token"           // Storage key for token
    },
    "csrf": {
      "enabled": false,
      "tokenEndpoint": "/api/csrf-token"
    }
  },
  
  // Feature toggles
  "features": {
    "includeValidation": true,           // Generate validation from FluentValidation
    "includeAuthentication": true,       // Generate auth helpers
    "generateReactHooks": true,          // Generate React hooks (React only)
    "generateNextJsHelpers": true        // Generate Next.js server helpers (Next.js only)
  },
  
  // Project discovery
  "discovery": {
    "autoDiscover": true,                // Auto-discover API projects
    "projectNamePatterns": [             // Project name patterns to include
      "*.Api",
      "*.WebApi"
    ],
    "namespacePatterns": [               // Namespace patterns to include
      "*.Controllers",
      "*.Endpoints"
    ]
  },
  
  // Advanced options
  "advanced": {
    "customTypeMappings": {              // Custom C# to TypeScript type mappings
      "System.Decimal": "number",
      "System.DateOnly": "string"
    },
    "excludedEndpoints": [               // Endpoints to exclude (regex patterns)
      "^/api/internal/.*"
    ],
    "excludedTypes": [                   // Types to exclude (regex patterns)
      ".*Internal.*"
    ]
  }
}
""";

    /// <summary>
    /// React-specific configuration template.
    /// </summary>
    public const string ReactTemplate = """
{
  "$schema": "https://codebridge.dev/schema.json",
  "solutionPath": "./YourSolution.sln",
  "output": {
    "path": "./src/api",
    "packageName": "@your-org/api-client",
    "packageVersion": "1.0.0"
  },
  "target": {
    "framework": "React",
    "language": "TypeScript",
    "moduleSystem": "ESM"
  },
  "api": {
    "baseUrl": "https://localhost:5001",
    "authentication": {
      "type": "Bearer",
      "storage": "LocalStorage"
    }
  },
  "features": {
    "includeValidation": true,
    "includeAuthentication": true,
    "generateReactHooks": true
  }
}
""";

    /// <summary>
    /// Next.js-specific configuration template.
    /// </summary>
    public const string NextJsTemplate = """
{
  "$schema": "https://codebridge.dev/schema.json",
  "solutionPath": "./YourSolution.sln",
  "output": {
    "path": "./lib/api",
    "packageName": "api-client",
    "packageVersion": "1.0.0"
  },
  "target": {
    "framework": "NextJs",
    "language": "TypeScript",
    "moduleSystem": "ESM"
  },
  "api": {
    "baseUrl": "https://localhost:5001",
    "authentication": {
      "type": "Bearer",
      "storage": "Cookie"
    }
  },
  "features": {
    "includeValidation": true,
    "includeAuthentication": true,
    "generateNextJsHelpers": true
  }
}
""";

    /// <summary>
    /// Build integration configuration template.
    /// </summary>
    public const string BuildIntegrationTemplate = """
{
  "$schema": "https://codebridge.dev/schema.json",
  "solutionPath": "./YourSolution.sln",
  "generation": {
    "mode": "BuildIntegration",
    "buildEvents": ["AfterBuild"]
  },
  "output": {
    "path": "./sdk",
    "packageName": "your-api-client",
    "packageVersion": "1.0.0"
  },
  "api": {
    "baseUrl": "https://localhost:5001"
  }
}
""";

    /// <summary>
    /// Watch mode configuration template.
    /// </summary>
    public const string WatchModeTemplate = """
{
  "$schema": "https://codebridge.dev/schema.json",
  "solutionPath": "./YourSolution.sln",
  "generation": {
    "mode": "Watch",
    "watchPaths": [
      "./src/**/*.cs",
      "./src/**/Controllers/*.cs",
      "./src/**/Endpoints/*.cs"
    ],
    "debounceMs": 500
  },
  "output": {
    "path": "./sdk",
    "packageName": "your-api-client",
    "packageVersion": "1.0.0"
  },
  "api": {
    "baseUrl": "https://localhost:5001"
  }
}
""";

    /// <summary>
    /// Gets a template by name.
    /// </summary>
    public static string GetTemplate(string templateName)
    {
        return templateName.ToLowerInvariant() switch
        {
            "minimal" or "min" => MinimalTemplate,
            "full" or "complete" => FullTemplate,
            "react" => ReactTemplate,
            "nextjs" or "next" => NextJsTemplate,
            "build" or "buildintegration" => BuildIntegrationTemplate,
            "watch" => WatchModeTemplate,
            _ => throw new ArgumentException($"Unknown template: {templateName}. Available templates: minimal, full, react, nextjs, build, watch")
        };
    }

    /// <summary>
    /// Gets all available template names.
    /// </summary>
    public static IReadOnlyList<string> GetAvailableTemplates()
    {
        return new[]
        {
            "minimal - Basic configuration to get started quickly",
            "full - Complete configuration with all options and comments",
            "react - React-specific configuration with hooks",
            "nextjs - Next.js-specific configuration with server helpers",
            "build - Build integration configuration",
            "watch - Watch mode configuration for development"
        };
    }
}
