# CLI Commands

CodeBridge provides a comprehensive command-line interface for SDK generation and project management.

## Global Options

Available for all commands:

| Option | Short | Description |
|--------|-------|-------------|
| `--config <path>` | `-c` | Path to configuration file (default: `codebridge.json`) |
| `--verbose` | `-v` | Enable verbose logging |
| `--help` | `-h` | Display help information |
| `--version` | | Display version information |

## Commands

### init

Initialize a new CodeBridge project with configuration file.

```bash
codebridge init [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--output <path>` | Configuration file path | `./codebridge.json` |
| `--force` | Overwrite existing configuration | `false` |
| `--template <name>` | Use configuration template | `default` |

**Examples:**

```bash
# Create default configuration
codebridge init

# Specify custom path
codebridge init --output ./config/sdk.json

# Overwrite existing configuration
codebridge init --force

# Use React template
codebridge init --template react
```

**Templates:**
- `default` - Standard configuration
- `react` - Optimized for React projects
- `nextjs` - Optimized for Next.js projects
- `vue` - Optimized for Vue projects
- `angular` - Optimized for Angular projects

---

### generate

Generate TypeScript SDK from .NET API.

```bash
codebridge generate [options]
```

**Options:**

| Option | Description |
|--------|-------------|
| `--project <path>` | Path to .csproj file |
| `--output <path>` | Override output directory |
| `--watch` | Watch for file changes |
| `--dry-run` | Preview without writing files |

**Examples:**

```bash
# Generate SDK
codebridge generate

# Specify project file
codebridge generate --project ./src/MyApi/MyApi.csproj

# Custom output directory
codebridge generate --output ./frontend/sdk

# Preview generation without writing
codebridge generate --dry-run

# Watch mode for development
codebridge generate --watch
```

---

### validate

Validate configuration file and project setup.

```bash
codebridge validate [options]
```

**Options:**

| Option | Description |
|--------|-------------|
| `--strict` | Enable strict validation |
| `--fix` | Auto-fix common issues |

**Examples:**

```bash
# Basic validation
codebridge validate

# Strict mode with all checks
codebridge validate --strict

# Validate and auto-fix
codebridge validate --fix
```

**Validation Checks:**
- ✅ Configuration syntax
- ✅ Required fields present
- ✅ Valid enum values
- ✅ File paths exist
- ✅ Type mappings valid
- ✅ Controllers have [GenerateSdk]
- ✅ Output directory writable

---

### watch

Watch for file changes and auto-regenerate SDK.

```bash
codebridge watch [options]
```

**Options:**

| Option | Description |
|--------|-------------|
| `--debounce <ms>` | Debounce delay in milliseconds |
| `--include <pattern>` | File patterns to watch |
| `--exclude <pattern>` | File patterns to ignore |

**Examples:**

```bash
# Watch with default settings
codebridge watch

# Custom debounce delay
codebridge watch --debounce 1000

# Watch specific files
codebridge watch --include "Controllers/**/*.cs"

# Exclude test files
codebridge watch --exclude "**/*.Tests.cs"
```

---

### clean

Clean generated SDK files.

```bash
codebridge clean [options]
```

**Options:**

| Option | Description |
|--------|-------------|
| `--dry-run` | Preview files to be deleted |
| `--force` | Skip confirmation prompt |

**Examples:**

```bash
# Clean with confirmation
codebridge clean

# Preview files to delete
codebridge clean --dry-run

# Skip confirmation
codebridge clean --force
```

---

### info

Display project and configuration information.

```bash
codebridge info [options]
```

**Options:**

| Option | Description |
|--------|-------------|
| `--json` | Output in JSON format |

**Examples:**

```bash
# Display info
codebridge info

# JSON output for scripting
codebridge info --json
```

**Displays:**
- CodeBridge version
- .NET SDK version
- Configuration file path
- Output directory
- Controllers found
- Endpoints discovered
- Generated files

---

## Common Workflows

### Development Workflow

```bash
# 1. Initialize project
codebridge init

# 2. Validate setup
codebridge validate

# 3. Generate SDK
codebridge generate

# 4. Watch for changes during development
codebridge watch
```

### CI/CD Pipeline

```bash
# 1. Validate configuration
codebridge validate --strict

# 2. Generate SDK
codebridge generate --dry-run

# 3. Generate for production
codebridge generate --config codebridge.production.json
```

### Clean Build

```bash
# 1. Clean existing SDK
codebridge clean --force

# 2. Regenerate
codebridge generate
```

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | General error |
| `2` | Invalid configuration |
| `3` | File system error |
| `4` | Validation failed |
| `5` | Generation failed |

Use in scripts:

```bash
if codebridge generate; then
  echo "SDK generated successfully"
else
  echo "SDK generation failed with code $?"
  exit 1
fi
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `CODEBRIDGE_CONFIG` | Default configuration file path |
| `CODEBRIDGE_OUTPUT` | Default output directory |
| `CODEBRIDGE_LOG_LEVEL` | Log level (`debug`, `info`, `warn`, `error`) |

**Example:**

```bash
export CODEBRIDGE_CONFIG=./config/sdk.json
export CODEBRIDGE_LOG_LEVEL=debug
codebridge generate
```

## Scripting Examples

### Bash Script

```bash
#!/bin/bash

# Generate SDK for multiple environments
for env in development staging production; do
  echo "Generating SDK for $env..."
  codebridge generate --config "codebridge.$env.json" --output "./sdk-$env"
done
```

### PowerShell Script

```powershell
# Generate and validate
codebridge generate
if ($LASTEXITCODE -eq 0) {
    Write-Host "SDK generated successfully" -ForegroundColor Green
    codebridge validate --strict
} else {
    Write-Host "SDK generation failed" -ForegroundColor Red
    exit 1
}
```

### Package.json Integration

```json
{
  "scripts": {
    "sdk:init": "codebridge init",
    "sdk:generate": "codebridge generate",
    "sdk:watch": "codebridge watch",
    "sdk:clean": "codebridge clean --force",
    "sdk:validate": "codebridge validate --strict"
  }
}
```

Then use:

```bash
npm run sdk:generate
```

## Next Steps

- [Configuration Guide](configuration.md) - Detailed configuration options
- [MSBuild Integration](msbuild-integration.md) - Automatic generation during build
- [Type Mapping](type-mapping.md) - Customize type conversions
