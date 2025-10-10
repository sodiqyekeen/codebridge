# CodeBridge 🌉

**Bridge the gap between your .NET backend and frontend applications**

CodeBridge is a powerful SDK generator that creates type-safe TypeScript/JavaScript clients for React and Next.js applications from your .NET APIs. No more manual API client coding - let CodeBridge do the heavy lifting!

[![NuGet CLI](https://img.shields.io/nuget/v/CodeBridge.Cli.svg?label=CLI)](https://www.nuget.org/packages/CodeBridge.Cli/)
[![NuGet Core](https://img.shields.io/nuget/v/CodeBridge.Core.svg?label=Core)](https://www.nuget.org/packages/CodeBridge.Core/)
[![NuGet MSBuild](https://img.shields.io/nuget/v/CodeBridge.MSBuild.svg?label=MSBuild)](https://www.nuget.org/packages/CodeBridge.MSBuild/)
[![Downloads](https://img.shields.io/nuget/dt/CodeBridge.Cli.svg)](https://www.nuget.org/packages/CodeBridge.Cli/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## ✨ Features

- 🎯 **Type-Safe** - Generate TypeScript types from your C# models
- ⚛️ **React Hooks** - Auto-generate custom hooks for your API endpoints
- 🚀 **Next.js Support** - Server Components and API routes helpers
- 🔄 **Watch Mode** - Regenerate SDK on code changes during development
- 🏗️ **Build Integration** - Optional MSBuild task for automatic generation
- ✅ **Validation** - Generate Zod schemas from FluentValidation rules
- 🎨 **Configurable** - Extensive customization options
- 📦 **npm Ready** - Generates publishable npm packages

## 🚀 Quick Start

### 1. Install the package

```bash
dotnet add package CodeBridge
```

### 2. Install the CLI tool

```bash
dotnet tool install -g CodeBridge.Cli
```

### 3. Initialize configuration

```bash
cd your-api-project
codebridge init --template react --output ./generated-sdk
```

This creates a `codebridge.json` configuration file. Customize it with your project paths and settings.

### 4. Add the [GenerateSdk] attribute (optional)

```csharp
[GenerateSdk] // Mark endpoints for SDK generation
[HttpGet]
public async Task<ActionResult<ProductResponse>> GetProduct(Guid id)
{
    // Your implementation
}
```

If you don't use attributes, CodeBridge will auto-discover all API endpoints.

### 5. Generate your SDK

```bash
codebridge generate
```

That's it! 🎉 Your TypeScript SDK is ready in the output folder with:
- ✅ `package.json` (with TypeScript and dependencies)
- ✅ `tsconfig.json` (configured for your project)
- ✅ `README.md` (usage documentation)
- ✅ Type-safe TypeScript code (API clients, hooks, types, validation)

## 📖 Documentation

- [Getting Started Guide](docs/getting-started.md)
- [Configuration Options](docs/configuration.md)
- [CLI Commands](docs/cli-commands.md)
- [Build Integration](docs/build-integration.md)
- [React Hooks](docs/react-hooks.md)
- [Next.js Integration](docs/nextjs-integration.md)

## 🎯 Usage Example

**Your .NET API:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> GetProducts()
    {
        // Your implementation
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        [FromBody] CreateProductCommand command)
    {
        // Your implementation
    }
}
```

**Generated TypeScript SDK:**
```typescript
// Auto-generated types
export interface ProductResponse {
  id: string;
  name: string;
  price: number;
  // ... more properties
}

export interface CreateProductCommand {
  name: string;
  price: number;
  // ... more properties
}

// Auto-generated API client
export async function getProducts(): Promise<ProductResponse[]> {
  const response = await httpClient.get('/api/products');
  return response.data;
}

export async function createProduct(
  command: CreateProductCommand
): Promise<ProductResponse> {
  const response = await httpClient.post('/api/products', command);
  return response.data;
}

// Auto-generated React hooks
export function useGetProducts() {
  return useQuery({
    queryKey: ['products'],
    queryFn: () => getProducts()
  });
}

export function useCreateProduct() {
  return useMutation({
    mutationFn: (data: CreateProductCommand) => createProduct(data)
  });
}
```

**Use in your React app:**
```tsx
import { useGetProducts, useCreateProduct } from '@yourorg/api-client';

function ProductList() {
  const { data: products, isLoading } = useGetProducts();
  const createProduct = useCreateProduct();

  // Fully type-safe! 🎉
}
```

## ⚙️ Configuration

Run `codebridge init` to create a `codebridge.json` configuration file, or create one manually:

```json
{
  "SolutionPath": "./MyApp.sln",
  "ProjectPaths": [],
  "Output": {
    "Path": "./generated-sdk",
    "PackageName": "@myapp/api-client",
    "PackageVersion": "1.0.0",
    "License": "MIT"
  },
  "Target": {
    "Framework": 0,
    "Language": "typescript",
    "ModuleSystem": 0
  },
  "Api": {
    "BaseUrl": "https://api.myapp.com",
    "Authentication": {
      "Type": 1,
      "Storage": 0
    }
  },
  "Features": {
    "GenerateReactHooks": true,
    "IncludeValidation": true,
    "IncludeAuthentication": true,
    "GenerateDocComments": true
  },
  "Generation": {
    "Mode": 0
  }
}
```

**Note:** Use `codebridge init --interactive` for a guided setup experience.

## 🔧 Generation Modes

### Manual (CLI)
```bash
codebridge generate
```

### Watch Mode
```bash
codebridge watch
```

### Build Integration
```json
{
  "generation": {
    "mode": "build-integration",
    "buildEvents": ["AfterBuild"]
  }
}
```

Now `dotnet build` automatically generates your SDK! 🚀

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Built with ❤️ using:
- [Roslyn](https://github.com/dotnet/roslyn) for C# code analysis
- [System.CommandLine](https://github.com/dotnet/command-line-api) for CLI
- Inspired by the needs of modern full-stack development

## 📬 Support

- 🐛 [Report a bug](https://github.com/sodiqyekeen/CodeBridge/issues)
- 💡 [Request a feature](https://github.com/sodiqyekeen/CodeBridge/issues)
- 📖 [Read the docs](https://github.com/sodiqyekeen/CodeBridge/wiki)

---

**CodeBridge** - Bridging .NET and Frontend, One SDK at a Time 🌉
