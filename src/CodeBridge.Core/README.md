# CodeBridge.Core

Core library for CodeBridge SDK generator. This package contains the core abstractions, services, and models used by CodeBridge to analyze .NET code and generate TypeScript SDKs.

## 📦 Installation

```bash
dotnet add package CodeBridge.Core
```

## 🎯 What's Included

### Services

- **SourceAnalyzer** - Roslyn-based C# code analyzer
  - Discovers endpoints with `[GenerateSdk]` attribute
  - Analyzes types and their properties
  - Discovers FluentValidation rules

- **TypeMapper** - C# to TypeScript type mapping
  - 50+ built-in type mappings
  - Generic type support (List<T>, Dictionary<K,V>, etc.)
  - Custom type mapping support
  - Nullable type handling

- **CodeGenerator** - TypeScript code generation
  - Interface generation
  - Enum generation
  - API client functions
  - Validation schemas (Zod)
  - React Query hooks
  - Next.js server actions

- **ConfigurationLoader** - Configuration management
  - JSON-based configuration
  - Environment-specific overrides
  - Validation and error reporting

### Models

- **EndpointInfo** - API endpoint metadata
- **TypeInfo** - Type information and properties
- **PropertyInfo** - Property details
- **PropertyValidationRules** - Validation rule metadata
- **CodeBridgeConfig** - Complete configuration model

### Attributes

- **GenerateSdkAttribute** - Mark endpoints for SDK generation
  ```csharp
  [GenerateSdk(
      Summary = "Get all products",
      Group = "products",
      RequiresAuth = true,
      Tags = new[] { "products", "catalog" }
  )]
  ```

## 🔧 Usage

### Analyze Source Code

```csharp
using CodeBridge.Core.Services;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var analyzer = new SourceAnalyzer(loggerFactory.CreateLogger<SourceAnalyzer>());

var projects = new List<ProjectInfo>
{
    new() { Name = "MyApi", Path = "/path/to/project" }
};

var endpoints = await analyzer.DiscoverEndpointsAsync(projects, cancellationToken);
var types = await analyzer.DiscoverTypesAsync(projects, cancellationToken);
```

### Map Types

```csharp
var typeMapper = new TypeMapper(logger, advancedOptions);

var tsType = typeMapper.MapToTypeScript("List<Product>"); 
// Result: "Product[]"

var tsType2 = typeMapper.MapToTypeScript("Dictionary<string, int>");
// Result: "Record<string, number>"
```

### Generate Code

```csharp
var generator = new CodeGenerator(logger, typeMapper, targetOptions, featureOptions);

// Generate TypeScript interface
var interfaceCode = await generator.GenerateTypeScriptInterfaceAsync(typeInfo, cancellationToken);

// Generate API function
var functionCode = await generator.GenerateApiClientFunctionAsync(endpoint, cancellationToken);

// Generate React hook
var hookCode = await generator.GenerateReactHookAsync(endpoint, cancellationToken);
```

### Load Configuration

```csharp
var configLoader = new ConfigurationLoader(logger);
var config = await configLoader.LoadAsync("codebridge.json", cancellationToken);

// Validate configuration
var errors = config.Validate();
if (errors.Count > 0)
{
    foreach (var error in errors)
    {
        Console.WriteLine(error);
    }
}
```

## 🎨 Type Mappings

### Primitives

| C# Type | TypeScript Type |
|---------|-----------------|
| `string` | `string` |
| `int`, `long`, `decimal`, `double`, `float` | `number` |
| `bool` | `boolean` |
| `DateTime`, `DateTimeOffset` | `string` |
| `Guid` | `string` |
| `void` | `void` |
| `object`, `dynamic` | `any` |

### Collections

| C# Type | TypeScript Type |
|---------|-----------------|
| `List<T>`, `T[]`, `IEnumerable<T>` | `T[]` |
| `Dictionary<string, T>` | `Record<string, T>` |
| `Dictionary<K, V>` | `Map<K, V>` |
| `HashSet<T>` | `Set<T>` |

### Special Types

| C# Type | TypeScript Type |
|---------|-----------------|
| `Result<T>` | `Result<T>` |
| `Task<T>`, `ActionResult<T>` | `T` (unwrapped) |
| `Nullable<T>`, `T?` | `T \| null` |
| `IFormFile` | `FormData` |
| `Stream` | `Blob` |

## 🔌 Extensibility

### Custom Type Mappings

```json
{
  "advanced": {
    "customTypeMappings": {
      "MyCustomType": "CustomTsType",
      "PagedResult<T>": "PagedResponse<T>"
    }
  }
}
```

## 📚 API Reference

For detailed API documentation, see the [API Reference](https://github.com/sodiqyekeen/CodeBridge/wiki/API-Reference).

## 🤝 Contributing

This is the core package that powers CodeBridge. Contributions are welcome!

## 📄 License

MIT License - see [LICENSE](../../LICENSE) for details.
