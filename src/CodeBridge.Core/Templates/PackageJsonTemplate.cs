using System.Text;
using System.Text.Json;
using CodeBridge.Core.Models.Configuration;

namespace CodeBridge.Core.Templates;

/// <summary>
/// Generates package.json with proper ESM exports, TypeScript support, and build scripts.
/// </summary>
public static class PackageJsonTemplate
{
    public static string Generate(CodeBridgeConfig config, List<string> featureGroups)
    {
        var packageJson = new
        {
            name = config.Output.PackageName,
            version = config.Output.Version ?? "1.0.0",
            description = config.Output.Description ?? "Auto-generated TypeScript SDK",
            main = "./dist/index.js",
            module = "./dist/index.js",
            types = "./dist/index.d.ts",
            type = "module",
            exports = GenerateExports(featureGroups),
            scripts = new Dictionary<string, string>
            {
                ["build"] = "tsc",
                ["build:watch"] = "tsc --watch",
                ["clean"] = "rimraf dist",
                ["prepublishOnly"] = "npm run clean && npm run build",
                ["typecheck"] = "tsc --noEmit"
            },
            files = new[]
            {
                "dist",
                "README.md",
                "LICENSE",
                "CHANGELOG.md"
            },
            keywords = new[]
            {
                "api",
                "sdk",
                "typescript",
                "client",
                "rest"
            },
            author = config.Output.Author ?? "",
            license = config.Output.License ?? "MIT",
            repository = string.IsNullOrEmpty(config.Output.Repository) ? null : new
            {
                type = "git",
                url = config.Output.Repository
            },
            bugs = string.IsNullOrEmpty(config.Output.Repository) ? null : new
            {
                url = $"{config.Output.Repository}/issues"
            },
            homepage = config.Output.Homepage ?? config.Output.Repository,
            peerDependencies = new Dictionary<string, string>
            {
                ["react"] = "^18.0.0 || ^19.0.0",
                ["react-dom"] = "^18.0.0 || ^19.0.0"
            },
            peerDependenciesMeta = new Dictionary<string, object>
            {
                ["react"] = new { optional = false },
                ["react-dom"] = new { optional = false }
            },
            dependencies = new Dictionary<string, string>
            {
                ["@tanstack/react-query"] = "^5.0.0"
            },
            devDependencies = new Dictionary<string, string>
            {
                ["typescript"] = "^5.0.0",
                ["rimraf"] = "^5.0.0",
                ["@types/react"] = "^18.0.0",
                ["@types/react-dom"] = "^18.0.0"
            },
            engines = new Dictionary<string, string>
            {
                ["node"] = ">=18.0.0",
                ["npm"] = ">=9.0.0"
            },
            sideEffects = false
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(packageJson, options);
    }

    private static object GenerateExports(List<string> featureGroups)
    {
        var exports = new Dictionary<string, object>
        {
            ["."] = new
            {
                types = "./dist/index.d.ts",
                import = "./dist/index.js",
                @default = "./dist/index.js"
            },
            ["./types"] = new
            {
                types = "./dist/types/index.d.ts",
                import = "./dist/types/index.js",
                @default = "./dist/types/index.js"
            },
            ["./lib"] = new
            {
                types = "./dist/lib/index.d.ts",
                import = "./dist/lib/index.js",
                @default = "./dist/lib/index.js"
            },
            ["./hooks"] = new
            {
                types = "./dist/hooks/index.d.ts",
                import = "./dist/hooks/index.js",
                @default = "./dist/hooks/index.js"
            },
            ["./validation"] = new
            {
                types = "./dist/validation/index.d.ts",
                import = "./dist/validation/index.js",
                @default = "./dist/validation/index.js"
            }
        };

        // Add feature group exports
        foreach (var group in featureGroups.Distinct())
        {
            var normalizedGroup = group.ToLowerInvariant().Replace(" ", "-");
            exports[$"./api/{normalizedGroup}"] = new
            {
                types = $"./dist/api/{normalizedGroup}.d.ts",
                import = $"./dist/api/{normalizedGroup}.js",
                @default = $"./dist/api/{normalizedGroup}.js"
            };
        }

        // Wildcard export for any other files
        exports["./package.json"] = "./package.json";

        return exports;
    }

    /// <summary>
    /// Generates tsconfig.json for the SDK project.
    /// </summary>
    public static string GenerateTsConfig()
    {
        var tsconfig = new
        {
            compilerOptions = new Dictionary<string, object>
            {
                ["target"] = "ES2020",
                ["module"] = "ESNext",
                ["lib"] = new[] { "ES2020", "DOM", "DOM.Iterable" },
                ["declaration"] = true,
                ["declarationMap"] = true,
                ["sourceMap"] = true,
                ["outDir"] = "./dist",
                ["rootDir"] = "./",
                ["removeComments"] = false,
                ["strict"] = true,
                ["noImplicitAny"] = true,
                ["strictNullChecks"] = true,
                ["strictFunctionTypes"] = true,
                ["strictBindCallApply"] = true,
                ["strictPropertyInitialization"] = true,
                ["noImplicitThis"] = true,
                ["alwaysStrict"] = true,
                ["noUnusedLocals"] = true,
                ["noUnusedParameters"] = true,
                ["noImplicitReturns"] = true,
                ["noFallthroughCasesInSwitch"] = true,
                ["esModuleInterop"] = true,
                ["skipLibCheck"] = true,
                ["forceConsistentCasingInFileNames"] = true,
                ["moduleResolution"] = "bundler",
                ["resolveJsonModule"] = true,
                ["jsx"] = "react",
                ["types"] = new[] { "node" }
            },
            include = new[]
            {
                "**/*.ts",
                "**/*.tsx"
            },
            exclude = new[]
            {
                "node_modules",
                "dist",
                "**/*.spec.ts",
                "**/*.test.ts"
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(tsconfig, options);
    }

    /// <summary>
    /// Generates .npmignore file.
    /// </summary>
    public static string GenerateNpmIgnore()
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Source files");
        sb.AppendLine("**/*.ts");
        sb.AppendLine("!**/*.d.ts");
        sb.AppendLine();
        sb.AppendLine("# Config files");
        sb.AppendLine("tsconfig.json");
        sb.AppendLine(".prettierrc");
        sb.AppendLine(".eslintrc");
        sb.AppendLine();
        sb.AppendLine("# Development");
        sb.AppendLine("node_modules/");
        sb.AppendLine(".git/");
        sb.AppendLine(".github/");
        sb.AppendLine("*.log");
        sb.AppendLine();
        sb.AppendLine("# Tests");
        sb.AppendLine("**/*.test.ts");
        sb.AppendLine("**/*.spec.ts");
        sb.AppendLine("coverage/");
        sb.AppendLine();
        sb.AppendLine("# Editor");
        sb.AppendLine(".vscode/");
        sb.AppendLine(".idea/");
        sb.AppendLine("*.swp");
        sb.AppendLine("*.swo");
        sb.AppendLine(".DS_Store");

        return sb.ToString();
    }

    /// <summary>
    /// Generates LICENSE file content.
    /// </summary>
    public static string GenerateLicense(string licenseType, string author, int year)
    {
        return licenseType?.ToUpperInvariant() switch
        {
            "MIT" => GenerateMitLicense(author, year),
            "APACHE-2.0" => "Apache License 2.0\n\nSee: https://www.apache.org/licenses/LICENSE-2.0",
            "GPL-3.0" => "GNU General Public License v3.0\n\nSee: https://www.gnu.org/licenses/gpl-3.0.en.html",
            _ => GenerateMitLicense(author, year)
        };
    }

    private static string GenerateMitLicense(string author, int year)
    {
        return $@"MIT License

Copyright (c) {year} {author}

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
";
    }

    /// <summary>
    /// Generates CHANGELOG.md template.
    /// </summary>
    public static string GenerateChangelog(string version)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Changelog");
        sb.AppendLine();
        sb.AppendLine("All notable changes to this project will be documented in this file.");
        sb.AppendLine();
        sb.AppendLine("The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),");
        sb.AppendLine("and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).");
        sb.AppendLine();
        sb.AppendLine($"## [{version}] - {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("### Added");
        sb.AppendLine();
        sb.AppendLine("- Initial release");
        sb.AppendLine("- TypeScript SDK with full type definitions");
        sb.AppendLine("- React hooks for all API endpoints");
        sb.AppendLine("- Automatic error handling");
        sb.AppendLine("- CSRF protection support");
        sb.AppendLine("- Token refresh with retry protection");
        sb.AppendLine("- File upload/download utilities");
        sb.AppendLine("- Client-side validation schemas");
        sb.AppendLine();

        return sb.ToString();
    }
}
