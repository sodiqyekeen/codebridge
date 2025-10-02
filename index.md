# CodeBridge Documentation

Welcome to **CodeBridge** - A powerful SDK generator that creates type-safe TypeScript clients from your .NET APIs.

## What is CodeBridge?

CodeBridge analyzes your .NET controllers and endpoints decorated with `[GenerateSdk]` attributes and automatically generates:

- 🎯 **Type-safe TypeScript interfaces** from your C# models
- 🚀 **Fully typed API client functions** for all endpoints
- ⚛️ **React Query hooks** for easy data fetching
- 📦 **Ready-to-use SDK packages** for your frontend

## Quick Start

### Installation

```bash
# Install the CLI tool globally
dotnet tool install -g CodeBridge.Cli

# Or add MSBuild integration to your project
dotnet add package CodeBridge.MSBuild
```

### Basic Usage

1. **Mark your API endpoints**:

```csharp
[ApiController]
[Route("api/[controller]")]
[GenerateSdk] // Add this attribute
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetProducts()
    {
        // Your implementation
    }
}
```

2. **Generate the SDK**:

```bash
codebridge generate
```

3. **Use in your frontend**:

```typescript
import { getProducts } from './generated/api';

const products = await getProducts();
// Fully typed with IntelliSense! 🎉
```

## Documentation

### 📚 Getting Started
- [Installation Guide](docs/getting-started/installation.md)
- [Quick Start Tutorial](docs/getting-started/quick-start.md)

### 📖 Guides
- [CLI Commands](docs/guides/cli-commands.md)
- [Configuration](docs/guides/configuration.md)
- [Type Mapping](docs/guides/type-mapping.md)
- [MSBuild Integration](docs/guides/msbuild-integration.md)

### 🔧 API Reference
- [CodeBridge.Core API](api/CodeBridge.Core.yml)
- [CodeBridge.Cli API](api/CodeBridge.Cli.yml)
- [CodeBridge.MSBuild API](api/CodeBridge.MSBuild.yml)

### 💡 Examples
- [React Example](docs/examples/react.md)
- [Next.js Example](docs/examples/nextjs.md)

## Features

✨ **Type Safety** - Generate TypeScript types from C# models  
🔄 **Auto-sync** - Keep your SDK in sync with your API  
⚡ **Fast** - Roslyn-powered analysis for quick generation  
🎨 **Customizable** - Extensive configuration options  
📦 **Framework Support** - React, Next.js, Vue, Angular, and more

## Community

- 🐙 [GitHub Repository](https://github.com/sodiqyekeen/codebridge)
- 🐛 [Report Issues](https://github.com/sodiqyekeen/codebridge/issues)
- 💬 [Discussions](https://github.com/sodiqyekeen/codebridge/discussions)

## License

CodeBridge is released under the [MIT License](https://github.com/sodiqyekeen/codebridge/blob/main/LICENSE).

---

**Ready to bridge your .NET API and frontend?** [Get Started →](docs/getting-started/installation.md)
