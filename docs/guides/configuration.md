# Configuration Guide

CodeBridge can be configured using a `codebridge.json` file in your project root. This guide covers all available configuration options.

## Creating Configuration

Initialize with default settings:

```bash
codebridge init
```

Or create manually:

```json
{
  "outputPath": "./generated",
  "baseUrl": "https://localhost:5001",
  "httpClient": "axios",
  "generateInterfaces": true,
  "generateEnums": true
}
```

## Configuration Schema

### Root Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `outputPath` | string | `"./generated"` | Directory where SDK files will be generated |
| `baseUrl` | string | `"https://localhost:5001"` | Base URL for API requests |
| `httpClient` | string | `"axios"` | HTTP client library (`"axios"`, `"fetch"`, `"custom"`) |
| `generateInterfaces` | boolean | `true` | Generate TypeScript interfaces for models |
| `generateEnums` | boolean | `true` | Generate TypeScript enums for C# enums |
| `prettier` | object | `null` | Prettier configuration for generated code |
| `typeMapping` | object | `{}` | Custom type mappings |
| `endpoints` | object | `{}` | Endpoint-specific overrides |

## HTTP Client Options

### Axios (Default)

```json
{
  "httpClient": "axios",
  "axios": {
    "timeout": 30000,
    "withCredentials": true,
    "headers": {
      "Content-Type": "application/json"
    }
  }
}
```

Generated code uses axios:

```typescript
import axios from 'axios';

export async function getProducts() {
  const response = await axios.get('/api/products');
  return response.data;
}
```

### Fetch API

```json
{
  "httpClient": "fetch",
  "fetch": {
    "credentials": "include",
    "headers": {
      "Content-Type": "application/json"
    }
  }
}
```

Generated code uses native fetch:

```typescript
export async function getProducts() {
  const response = await fetch('/api/products');
  return response.json();
}
```

### Custom HTTP Client

```json
{
  "httpClient": "custom",
  "customClient": {
    "importPath": "@/lib/api-client",
    "functionName": "apiRequest"
  }
}
```

Generated code uses your custom client:

```typescript
import { apiRequest } from '@/lib/api-client';

export async function getProducts() {
  return apiRequest<Product[]>('GET', '/api/products');
}
```

## Type Mapping

Override default C# to TypeScript type mappings:

```json
{
  "typeMapping": {
    "System.Guid": "string",
    "System.DateTime": "Date",
    "System.Decimal": "number",
    "MyApp.Models.CustomType": "CustomInterface"
  }
}
```

Default mappings:

| C# Type | TypeScript Type |
|---------|-----------------|
| `string` | `string` |
| `int`, `long`, `float`, `double`, `decimal` | `number` |
| `bool` | `boolean` |
| `DateTime` | `string` (ISO 8601) |
| `Guid` | `string` |
| `List<T>`, `IEnumerable<T>` | `T[]` |
| `Dictionary<K, V>` | `Record<K, V>` |

## Prettier Configuration

Format generated code with Prettier:

```json
{
  "prettier": {
    "semi": true,
    "singleQuote": true,
    "trailingComma": "es5",
    "tabWidth": 2,
    "printWidth": 100
  }
}
```

## Endpoint Configuration

Override settings per endpoint:

```json
{
  "endpoints": {
    "Products": {
      "baseUrl": "https://api.products.example.com",
      "timeout": 60000
    },
    "Orders": {
      "baseUrl": "https://api.orders.example.com",
      "authentication": "bearer"
    }
  }
}
```

## Environment-Specific Configuration

### Development

**codebridge.development.json:**
```json
{
  "baseUrl": "https://localhost:5001",
  "axios": {
    "timeout": 10000
  }
}
```

### Production

**codebridge.production.json:**
```json
{
  "baseUrl": "https://api.example.com",
  "axios": {
    "timeout": 30000,
    "withCredentials": true
  }
}
```

Use with CLI:

```bash
codebridge generate --config codebridge.production.json
```

## Advanced Options

### Output Customization

```json
{
  "output": {
    "typesFile": "types.ts",
    "apiFile": "api.ts",
    "indexFile": "index.ts",
    "splitByController": true
  }
}
```

When `splitByController` is `true`, generates:

```
generated/
├── types.ts
├── products-api.ts
├── orders-api.ts
└── index.ts
```

### Naming Conventions

```json
{
  "naming": {
    "interfaces": "PascalCase",
    "functions": "camelCase",
    "enums": "PascalCase"
  }
}
```

## Full Example

**codebridge.json:**
```json
{
  "outputPath": "./src/generated",
  "baseUrl": "https://api.example.com",
  "httpClient": "axios",
  "generateInterfaces": true,
  "generateEnums": true,
  "prettier": {
    "semi": true,
    "singleQuote": true,
    "trailingComma": "es5",
    "tabWidth": 2
  },
  "axios": {
    "timeout": 30000,
    "withCredentials": true,
    "headers": {
      "Content-Type": "application/json",
      "Accept": "application/json"
    }
  },
  "typeMapping": {
    "System.Guid": "string",
    "System.DateTime": "string"
  },
  "output": {
    "splitByController": true
  },
  "naming": {
    "interfaces": "PascalCase",
    "functions": "camelCase"
  }
}
```

## Validation

Validate your configuration:

```bash
codebridge validate
```

This checks:
- JSON syntax
- Required fields
- Valid enum values
- File paths exist
- Type mapping correctness

## Schema IntelliSense

For IntelliSense in VS Code, add a `$schema` property:

```json
{
  "$schema": "https://raw.githubusercontent.com/YOUR_USERNAME/CodeBridge/main/schema.json",
  "outputPath": "./generated"
}
```

## Next Steps

- [CLI Commands](cli-commands.md) - All available CLI commands
- [Type Mapping](type-mapping.md) - Detailed type mapping guide
- [Custom Templates](custom-templates.md) - Create custom code templates
