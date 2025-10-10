# Quick Start Tutorial

Get started with CodeBridge in 5 minutes! This tutorial will guide you through generating your first TypeScript SDK.

## Step 1: Create a .NET Web API

If you don't have an existing API, create one:

```bash
dotnet new webapi -n MyApi
cd MyApi
```

## Step 2: Install CodeBridge

Add CodeBridge to your project:

```bash
dotnet add package CodeBridge.MSBuild
```

Or install the CLI tool:

```bash
dotnet tool install -g CodeBridge.Cli
```

## Step 3: Add the GenerateSdk Attribute

Add the `GenerateSdk` attribute to your controllers:

```csharp
using CodeBridge.Core.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[GenerateSdk] // 👈 Add this attribute
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetProducts()
    {
        return Ok(new List<Product>
        {
            new() { Id = 1, Name = "Product 1", Price = 29.99m },
            new() { Id = 2, Name = "Product 2", Price = 49.99m }
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = new Product { Id = id, Name = $"Product {id}", Price = 29.99m };
        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Random.Shared.Next(1000),
            Name = request.Name,
            Price = request.Price
        };
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }
}

public record Product
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public decimal Price { get; init; }
}

public record CreateProductRequest
{
    public required string Name { get; init; }
    public decimal Price { get; init; }
}
```

## Step 4: Initialize Configuration

Create a configuration file:

```bash
codebridge init --template react --output ./generated-sdk
```

This creates a `codebridge.json` file. Customize it with your project details:

```json
{
  "SolutionPath": "./MyApi.sln",
  "ProjectPaths": [],
  "Output": {
    "Path": "./generated-sdk",
    "PackageName": "@myorg/api-client",
    "PackageVersion": "1.0.0",
    "License": "MIT"
  },
  "Target": {
    "Framework": 0,
    "Language": "typescript",
    "ModuleSystem": 0
  },
  "Api": {
    "BaseUrl": "https://localhost:5001"
  },
  "Features": {
    "GenerateReactHooks": true,
    "IncludeValidation": true
  }
}
```

## Step 5: Generate the SDK

### Using CLI

```bash
codebridge generate
```

### Using MSBuild (Automatic)

```bash
dotnet build
```

The SDK will be automatically generated during the build process.

## Step 6: View the Generated SDK

Navigate to the output folder (default: `./generated-sdk`):

```
generated-sdk/
├── package.json         # npm package with dependencies
├── tsconfig.json        # TypeScript configuration  
├── README.md           # Usage documentation
├── index.ts            # Main export file
├── api/
│   ├── products.ts     # Products API client
│   └── index.ts
├── hooks/              # React Query hooks (if enabled)
│   ├── useGetProducts.ts
│   ├── useCreateProduct.ts
│   └── index.ts
├── types/              # TypeScript interfaces
│   ├── product.ts
│   ├── createProductRequest.ts
│   └── index.ts
├── lib/                # HTTP client utilities
│   ├── httpService.ts
│   └── index.ts
└── validation/         # Zod schemas (if enabled)
    └── index.ts
```

### Example Generated Code

**types/product.ts:**
```typescript
export interface Product {
  id: number;
  name: string;
  price: number;
}
```

**api/products.ts:**
```typescript
import http from '../lib/httpService';
import type { Product, CreateProductRequest } from '../types';

export async function getProductsAsync(): Promise<Product[]> {
  return http.get<Product[]>('/api/Products');
}

export async function getProductAsync(id: number): Promise<Product> {
  return http.get<Product>(`/api/Products/${id}`);
}

export async function createProductAsync(data: CreateProductRequest): Promise<Product> {
  return http.post<Product>('/api/Products', data);
}
```

**hooks/useGetProducts.ts:** (if React hooks enabled)
```typescript
import { useQuery } from '@tanstack/react-query';
import { getProductsAsync } from '../api/products';

export function useGetProducts() {
  return useQuery({
    queryKey: ['products'],
    queryFn: () => getProductsAsync()
  });
}
```

## Step 7: Install and Use in Your Frontend

### Install the SDK

```bash
cd generated-sdk
npm install
```

This installs all dependencies including TypeScript, axios, and React Query (if enabled).

### React Example with Hooks

```typescript
import { useGetProducts, useCreateProduct } from './generated-sdk';
import type { CreateProductRequest } from './generated-sdk';

function ProductList() {
  const { data: products, isLoading } = useGetProducts();
  const createProduct = useCreateProduct();

  const handleCreate = async () => {
    const newProduct: CreateProductRequest = {
      name: 'New Product',
      price: 99.99
    };
    
    await createProduct.mutateAsync(newProduct);
  };

  if (isLoading) return <div>Loading...</div>;

  return (
    <div>
      <button onClick={handleCreate}>Create Product</button>
      <ul>
        {products?.map(product => (
          <li key={product.id}>
            {product.name} - ${product.price}
          </li>
        ))}
      </ul>
    </div>
  );
}
```

### Direct API Usage (without hooks)

```typescript
import { getProductsAsync, createProductAsync } from './generated-sdk';

async function loadProducts() {
  const products = await getProductsAsync();
  console.log(products);
  
  const newProduct = await createProductAsync({
    name: 'New Product',
    price: 99.99
  });
  console.log(newProduct);
}
```

## What's Next?

- 📚 [Configuration Guide](../guides/configuration.md) - Customize output, HTTP client, and more
- 🎯 [Controller Attributes](../guides/controller-attributes.md) - Advanced attribute options
- 🔄 [Type Mapping](../guides/type-mapping.md) - How C# types map to TypeScript
- 📦 [CLI Commands](../guides/cli-commands.md) - All available commands

## Tips

1. **Watch Mode**: Use `codebridge watch` during development to auto-regenerate on file changes
2. **Validation**: Run `codebridge validate` to check your configuration before generating
3. **Multiple Controllers**: Add `[GenerateSdk]` to as many controllers as you want (or use auto-discovery)
4. **TypeScript Ready**: Generated SDK includes `package.json` and `tsconfig.json` - just run `npm install`!
5. **Zero Errors**: All generated code is fully type-safe with zero TypeScript compilation errors
6. **Route Parameters**: Endpoints with route parameters (`{id}`) automatically generate functions with those parameters

Congratulations! 🎉 You've successfully generated your first TypeScript SDK with CodeBridge.
