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

## Step 4: Initialize Configuration (Optional)

Create a configuration file for customization:

```bash
codebridge init
```

This creates a `codebridge.json` file with default settings:

```json
{
  "outputPath": "./generated",
  "baseUrl": "https://localhost:5001",
  "httpClient": "axios",
  "generateInterfaces": true,
  "generateEnums": true
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

Navigate to the output folder (default: `./generated`):

```
generated/
├── types.ts          # TypeScript interfaces for all models
├── api.ts            # API client functions
└── index.ts          # Main export file
```

### Example Generated Code

**types.ts:**
```typescript
export interface Product {
  id: number;
  name: string;
  price: number;
}

export interface CreateProductRequest {
  name: string;
  price: number;
}
```

**api.ts:**
```typescript
import axios from 'axios';
import type { Product, CreateProductRequest } from './types';

const baseURL = 'https://localhost:5001';

export async function getProducts(): Promise<Product[]> {
  const response = await axios.get<Product[]>(`${baseURL}/api/Products`);
  return response.data;
}

export async function getProduct(id: number): Promise<Product> {
  const response = await axios.get<Product>(`${baseURL}/api/Products/${id}`);
  return response.data;
}

export async function createProduct(request: CreateProductRequest): Promise<Product> {
  const response = await axios.post<Product>(`${baseURL}/api/Products`, request);
  return response.data;
}
```

## Step 7: Use in Your Frontend

### React Example

```typescript
import { useEffect, useState } from 'react';
import { getProducts, type Product } from './generated';

function ProductList() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getProducts()
      .then(setProducts)
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div>Loading...</div>;

  return (
    <ul>
      {products.map(product => (
        <li key={product.id}>
          {product.name} - ${product.price}
        </li>
      ))}
    </ul>
  );
}
```

### Next.js Example

```typescript
// app/products/page.tsx
import { getProducts } from '@/generated';

export default async function ProductsPage() {
  const products = await getProducts();

  return (
    <div>
      <h1>Products</h1>
      <ul>
        {products.map(product => (
          <li key={product.id}>
            {product.name} - ${product.price}
          </li>
        ))}
      </ul>
    </div>
  );
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
3. **Multiple Controllers**: Add `[GenerateSdk]` to as many controllers as you want
4. **Custom Base URL**: Set `baseUrl` in `codebridge.json` for different environments

Congratulations! 🎉 You've successfully generated your first TypeScript SDK with CodeBridge.
