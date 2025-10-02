# CodeBridge CLI

Command-line interface for CodeBridge SDK generator. Generate TypeScript SDKs from your .NET APIs with simple commands.

## 📦 Installation

### Global Installation

```bash
dotnet tool install -g CodeBridge.Cli
```

### Local Installation

```bash
dotnet new tool-manifest
dotnet tool install CodeBridge.Cli
```

### Update

```bash
dotnet tool update -g CodeBridge.Cli
```

## 🚀 Commands

### `init` - Initialize Configuration

Create a new `codebridge.json` configuration file with interactive prompts.

```bash
codebridge init [options]
```

**Options:**
- `-t, --template <template>` - Template to use (react, nextjs, vue, angular, vanilla)
- `-o, --output <output>` - Output directory [default: ./generated-sdk]
- `-u, --api-url <url>` - Base URL of the API [default: https://localhost:5001]
- `-i, --interactive` - Run in interactive mode [default: true]

**Examples:**

```bash
# Interactive mode (default)
codebridge init

# Non-interactive with React template
codebridge init -t react -o ../frontend/sdk -u https://api.myapp.com --interactive false

# Next.js project
codebridge init --template nextjs --output ./app/lib/sdk
```

### `generate` - Generate SDK

Generate TypeScript SDK from your .NET API.

```bash
codebridge generate [options]
```

**Options:**
- `-c, --config <path>` - Path to codebridge.json [default: ./codebridge.json]
- `-v, --verbose` - Enable verbose logging [default: false]
- `-w, --watch` - Watch for changes and regenerate [default: false]

**Examples:**

```bash
# Generate with default config
codebridge generate

# Generate with custom config
codebridge generate -c ./config/codebridge.json

# Generate with verbose logging
codebridge generate --verbose

# Watch mode (regenerate on changes)
codebridge generate --watch
```

### `validate` - Validate Configuration

Validate your `codebridge.json` configuration file.

```bash
codebridge validate [options]
```

**Options:**
- `-c, --config <path>` - Path to codebridge.json [default: ./codebridge.json]

**Examples:**

```bash
# Validate default config
codebridge validate

# Validate custom config
codebridge validate -c ./config/codebridge.json
```

## 📝 Configuration File

The `codebridge.json` file controls SDK generation:

```json
{
  "solutionPath": "./MyApi.sln",
  "projectPaths": [],
  "output": {
    "path": "./generated-sdk",
    "packageName": "@myorg/api-client",
    "packageVersion": "1.0.0",
    "author": "Your Name",
    "description": "Auto-generated SDK for MyApi",
    "license": "MIT",
    "repository": "https://github.com/myorg/myapi",
    "cleanBeforeGenerate": true
  },
  "target": {
    "framework": "React",
    "language": "typescript",
    "moduleSystem": "ESM"
  },
  "api": {
    "baseUrl": "https://api.myapp.com",
    "authentication": {
      "type": "Bearer"
    }
  },
  "features": {
    "includeValidation": true,
    "generateReactHooks": true,
    "generateNextJsHelpers": false
  },
  "discovery": {
    "autoDiscover": true,
    "projectNamePatterns": [
      "*.Api.csproj",
      "*.WebApi.csproj"
    ]
  },
  "generation": {
    "mode": "Manual"
  },
  "advanced": {
    "customTypeMappings": {}
  }
}
```

## 🎯 Workflows

### Initial Setup

```bash
# 1. Navigate to your API project
cd MyApi

# 2. Initialize configuration
codebridge init

# 3. Review and customize codebridge.json
# Edit the file as needed

# 4. Mark your endpoints
# Add [GenerateSdk] attributes to your controllers

# 5. Generate SDK
codebridge generate

# 6. Use in your frontend
cd ../frontend
npm install ../generated-sdk
```

### Development Workflow

```bash
# Terminal 1: Watch mode
cd MyApi
codebridge generate --watch

# Terminal 2: Your frontend dev server
cd frontend
npm run dev

# Now API changes automatically regenerate the SDK!
```

### CI/CD Integration

```bash
# In your build script
dotnet build
codebridge generate --verbose

# Or fail build on validation errors
codebridge validate || exit 1
codebridge generate --verbose
```

## 🔍 Output Structure

Generated SDK structure:

```
generated-sdk/
├── types/              # TypeScript interfaces & enums
│   ├── product.ts
│   ├── customer.ts
│   └── ...
├── api/                # API client functions
│   ├── products.ts
│   ├── customers.ts
│   └── ...
├── validation/         # Zod validation schemas
│   ├── createProduct.schema.ts
│   └── ...
├── hooks/              # React Query hooks (if enabled)
│   ├── useGetProducts.ts
│   ├── useCreateProduct.ts
│   └── ...
├── server/             # Next.js server actions (if enabled)
│   ├── getProducts.server.ts
│   └── ...
├── package.json
└── README.md
```

## 📊 Progress Output

Generation shows progress with 4 phases:

```
🚀 Starting SDK generation...

📊 Phase 1/4: Analyzing source code...
   ✓ Found 25 endpoints
   ✓ Found 40 types

📋 Phase 2/4: Analyzing validation rules...
   ✓ Found validation rules for 15 types

✨ Phase 3/4: Generating TypeScript code...
   ✓ Generated 40 type files
   ✓ Generated 5 API client files
   ✓ Generated validation schemas
   ⚛️  Generated 25 React hooks

📦 Phase 4/4: Generating package files...
   ✓ Generated package.json
   ✓ Generated README.md

✅ SDK generated successfully in 2.34s
📁 Output: /path/to/generated-sdk
```

## 🐛 Troubleshooting

### "Configuration file not found"

```bash
# Ensure codebridge.json exists
codebridge validate

# Or specify path explicitly
codebridge generate -c path/to/codebridge.json
```

### "No endpoints with [GenerateSdk] found"

Make sure your API endpoints have the `[GenerateSdk]` attribute:

```csharp
[HttpGet]
[GenerateSdk(Summary = "Get all products")]
public async Task<Result<List<Product>>> GetAll() { }
```

### Watch mode not detecting changes

Ensure you're watching the correct directory and file patterns are correct.

## 📚 Additional Resources

- [Full Documentation](https://github.com/sodiqyekeen/CodeBridge)
- [Configuration Reference](https://github.com/sodiqyekeen/CodeBridge/wiki/Configuration)
- [Examples](https://github.com/sodiqyekeen/CodeBridge/tree/main/examples)

## 📄 License

MIT License - see [LICENSE](../../LICENSE) for details.
