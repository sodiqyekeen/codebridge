# MSBuild Integration

CodeBridge integrates seamlessly with MSBuild to automatically generate TypeScript SDKs during your project build.

## Installation

Add the MSBuild package to your .NET project:

```bash
dotnet add package CodeBridge.MSBuild
```

## How It Works

CodeBridge.MSBuild runs as part of your build pipeline:

```
Compile → CodeBridge SDK Generation → Build Complete
```

The SDK is generated **after** compilation, ensuring all types are available.

## Configuration

### Basic Setup

**codebridge.json:**
```json
{
  "outputPath": "./generated",
  "baseUrl": "https://localhost:5001",
  "httpClient": "axios"
}
```

### MSBuild Properties

Configure in your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    
    <!-- CodeBridge MSBuild Properties -->
    <CodeBridgeEnabled>true</CodeBridgeEnabled>
    <CodeBridgeConfigFile>codebridge.json</CodeBridgeConfigFile>
    <CodeBridgeOutputPath>$(SolutionDir)frontend/src/api</CodeBridgeOutputPath>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="CodeBridge.MSBuild" Version="1.0.0" />
  </ItemGroup>
</Project>
```

## Available Properties

| Property | Default | Description |
|----------|---------|-------------|
| `CodeBridgeEnabled` | `true` | Enable/disable SDK generation |
| `CodeBridgeConfigFile` | `codebridge.json` | Path to configuration file |
| `CodeBridgeOutputPath` | `./generated` | Output directory for SDK |
| `CodeBridgeVerbose` | `false` | Enable verbose logging |
| `CodeBridgeFailOnError` | `true` | Fail build if generation fails |

## Conditional Generation

### Development Only

Generate SDK only in Debug configuration:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <CodeBridgeEnabled>true</CodeBridgeEnabled>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <CodeBridgeEnabled>false</CodeBridgeEnabled>
</PropertyGroup>
```

### CI/CD Pipelines

Disable during CI builds if SDK is generated separately:

```xml
<PropertyGroup Condition="'$(CI)' == 'true'">
  <CodeBridgeEnabled>false</CodeBridgeEnabled>
</PropertyGroup>
```

## Build Targets

CodeBridge adds custom MSBuild targets:

### GenerateSdk Target

Manually trigger SDK generation:

```bash
dotnet build /t:GenerateSdk
```

### CleanSdk Target

Clean generated SDK files:

```bash
dotnet build /t:CleanSdk
```

### ValidateSdk Target

Validate configuration without generating:

```bash
dotnet build /t:ValidateSdk
```

## Integration Scenarios

### Monorepo Structure

**Project structure:**
```
myapp/
├── backend/
│   ├── MyApi.csproj
│   └── codebridge.json
└── frontend/
    └── src/
        └── api/  ← Generated SDK here
```

**codebridge.json:**
```json
{
  "outputPath": "../frontend/src/api"
}
```

### Multiple Frontend Projects

**MyApi.csproj:**
```xml
<PropertyGroup>
  <CodeBridgeConfigs>
    codebridge.react.json;
    codebridge.mobile.json;
    codebridge.admin.json
  </CodeBridgeConfigs>
</PropertyGroup>
```

Each config targets a different output:

**codebridge.react.json:**
```json
{
  "outputPath": "../frontend-react/src/api"
}
```

**codebridge.mobile.json:**
```json
{
  "outputPath": "../mobile-app/src/api"
}
```

### Docker Build

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src
COPY . .

# Restore and build (SDK generation happens automatically)
RUN dotnet restore
RUN dotnet build --no-restore

# Copy generated SDK to output
RUN cp -r generated /output/sdk

FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY --from=build /output /app
```

## Build Events

### Pre-Build Validation

Validate before building:

```xml
<Target Name="ValidateCodeBridgeConfig" BeforeTargets="Build">
  <Exec Command="codebridge validate" />
</Target>
```

### Post-Build Notification

Notify after SDK generation:

```xml
<Target Name="NotifyCodeBridgeComplete" AfterTargets="GenerateSdk">
  <Message Importance="high" Text="✅ TypeScript SDK generated successfully!" />
</Target>
```

### Copy to Multiple Locations

```xml
<Target Name="CopyGeneratedSdk" AfterTargets="GenerateSdk">
  <ItemGroup>
    <GeneratedFiles Include="generated/**/*" />
  </ItemGroup>
  
  <Copy SourceFiles="@(GeneratedFiles)"
        DestinationFolder="../frontend/src/api/%(RecursiveDir)" />
  <Copy SourceFiles="@(GeneratedFiles)"
        DestinationFolder="../mobile/src/api/%(RecursiveDir)" />
</Target>
```

## Troubleshooting

### SDK Not Generated

1. Check if CodeBridge is enabled:
```bash
dotnet build /p:CodeBridgeVerbose=true
```

2. Verify configuration file exists:
```bash
ls -la codebridge.json
```

3. Validate configuration:
```bash
codebridge validate
```

### Build Performance

SDK generation adds ~1-5 seconds to build time. Optimize:

**Option 1: Disable in Release**
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <CodeBridgeEnabled>false</CodeBridgeEnabled>
</PropertyGroup>
```

**Option 2: Skip when not needed**
```bash
dotnet build /p:CodeBridgeEnabled=false
```

**Option 3: Generate separately**
```bash
# Build without generation
dotnet build /p:CodeBridgeEnabled=false

# Generate SDK separately
codebridge generate
```

### Concurrent Builds

Multiple developers building simultaneously? Use file locks:

```json
{
  "output": {
    "useLocking": true,
    "lockTimeout": 30000
  }
}
```

## IDE Integration

### Visual Studio

CodeBridge integrates with Visual Studio build process:

1. Build → SDK generated automatically
2. Output window shows CodeBridge logs
3. Error List shows any generation errors

### Rider

JetBrains Rider shows CodeBridge in Build Output:

1. View → Tool Windows → Build
2. CodeBridge logs appear during build
3. Errors link to source files

### VS Code

With C# Dev Kit:

1. Terminal → Run Build Task
2. CodeBridge runs as part of build
3. Problems panel shows errors

## Best Practices

### 1. Commit Configuration, Not Generated Code

**.gitignore:**
```
generated/
frontend/src/api/
```

**Keep:**
```
codebridge.json
```

Each developer generates their own SDK during build.

### 2. Validate in CI

```yaml
# GitHub Actions
- name: Validate CodeBridge Config
  run: |
    dotnet tool install -g CodeBridge.Cli
    codebridge validate --strict
```

### 3. Version Lock

```xml
<PackageReference Include="CodeBridge.MSBuild" Version="1.0.0" />
```

Don't use floating versions in production.

### 4. Monitor Build Times

```xml
<PropertyGroup>
  <CodeBridgeVerbose>true</CodeBridgeVerbose>
</PropertyGroup>
```

Check logs for generation time.

## Next Steps

- [Configuration Guide](configuration.md) - Full configuration options
- [CLI Commands](cli-commands.md) - Manual SDK generation
- [Type Mapping](type-mapping.md) - Customize type conversions
