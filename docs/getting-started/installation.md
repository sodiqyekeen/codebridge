# Installation Guide

## Prerequisites

- .NET 8.0 or later SDK
- Node.js 18+ (for using generated SDKs)
- TypeScript 5.0+ (recommended)

## Installation Methods

### Method 1: Global CLI Tool (Recommended)

Install CodeBridge CLI globally to use it across all your projects:

```bash
dotnet tool install -g CodeBridge.Cli
```

Verify installation:

```bash
codebridge --version
```

Update to latest version:

```bash
dotnet tool update -g CodeBridge.Cli
```

### Method 2: MSBuild Integration

Add CodeBridge to your .NET project for automatic SDK generation during build:

```bash
dotnet add package CodeBridge.MSBuild
```

This will automatically generate SDKs whenever you build your project.

### Method 3: NuGet Package Manager

Using Visual Studio:
1. Right-click on your project
2. Select "Manage NuGet Packages"
3. Search for "CodeBridge.MSBuild"
4. Click "Install"

## Verify Installation

### For CLI Tool

```bash
codebridge --help
```

You should see a list of available commands.

### For MSBuild Package

Build your project:

```bash
dotnet build
```

Look for CodeBridge output in the build logs. If configured correctly, you'll see messages about SDK generation.

## Next Steps

- [Quick Start Tutorial](quick-start.md) - Create your first SDK in 5 minutes
- [Configuration](../guides/configuration.md) - Customize CodeBridge for your needs
- [CLI Commands](../guides/cli-commands.md) - Learn all available commands

## Troubleshooting

### "Command not found: codebridge"

Make sure .NET tools are in your PATH:

```bash
# Add to ~/.zshrc or ~/.bashrc
export PATH="$PATH:$HOME/.dotnet/tools"
```

Then restart your terminal.

### MSBuild Integration Not Working

1. Ensure you've added the package to your API project (not the client project)
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check for configuration file: `codebridge.json` should exist in your project root

### Version Conflicts

If you encounter version conflicts:

```bash
# Uninstall existing version
dotnet tool uninstall -g CodeBridge.Cli

# Reinstall latest
dotnet tool install -g CodeBridge.Cli
```

## Offline Installation

For air-gapped environments, download the NuGet packages manually:

1. Visit [NuGet.org](https://www.nuget.org/packages/CodeBridge.Cli)
2. Download the `.nupkg` files
3. Install from local source:

```bash
dotnet tool install -g CodeBridge.Cli --add-source ./path/to/packages
```
