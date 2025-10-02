# CodeBridge MSBuild Integration

This package provides MSBuild integration for CodeBridge, allowing automatic SDK generation during build.

## Installation

```bash
dotnet add package CodeBridge.MSBuild
```

## Usage

### Automatic Integration

Once installed, CodeBridge will automatically run during build if a `codebridge.json` file exists in your project directory.

### Configuration

Control CodeBridge behavior via MSBuild properties:

```xml
<PropertyGroup>
  <!-- Enable/disable CodeBridge -->
  <CodeBridgeEnabled>true</CodeBridgeEnabled>
  
  <!-- Path to configuration file -->
  <CodeBridgeConfigFile>$(MSBuildProjectDirectory)\codebridge.json</CodeBridgeConfigFile>
  
  <!-- When to run: BeforeBuild or AfterBuild -->
  <CodeBridgeBuildEvent>BeforeBuild</CodeBridgeBuildEvent>
  
  <!-- Enable incremental generation (skip if no changes) -->
  <CodeBridgeIncremental>true</CodeBridgeIncremental>
  
  <!-- Enable verbose logging -->
  <CodeBridgeVerbose>false</CodeBridgeVerbose>
</PropertyGroup>
```

### Manual Generation

Generate SDK manually using MSBuild target:

```bash
dotnet msbuild /t:CodeBridgeGenerate
```

### Disable for Specific Configurations

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <CodeBridgeEnabled>false</CodeBridgeEnabled>
</PropertyGroup>
```

## Build Events

- **BeforeBuild**: Generate SDK before compilation (default)
- **AfterBuild**: Generate SDK after successful build
- **Manual**: Only generate when explicitly called

## Features

- ✅ Automatic generation during build
- ✅ Incremental generation (skip if no changes)
- ✅ Configurable build events
- ✅ Verbose logging support
- ✅ Manual generation target
- ✅ Clean integration (removes generated files)
- ✅ Cancellation support (Ctrl+C)

## Example

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CodeBridge.MSBuild" Version="1.0.0" />
  </ItemGroup>

  <!-- Optional: Customize CodeBridge behavior -->
  <PropertyGroup>
    <CodeBridgeBuildEvent>AfterBuild</CodeBridgeBuildEvent>
    <CodeBridgeVerbose>true</CodeBridgeVerbose>
  </PropertyGroup>

</Project>
```
