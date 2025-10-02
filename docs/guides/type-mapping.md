# Type Mapping

CodeBridge automatically maps C# types to their TypeScript equivalents. This guide explains the mapping rules and how to customize them.

## Default Type Mappings

### Primitive Types

| C# Type | TypeScript Type |
|---------|-----------------|
| `string` | `string` |
| `int`, `long`, `short`, `byte` | `number` |
| `float`, `double`, `decimal` | `number` |
| `bool` | `boolean` |
| `char` | `string` |
| `object` | `any` |
| `void` | `void` |

### Common .NET Types

| C# Type | TypeScript Type |
|---------|-----------------|
| `DateTime`, `DateTimeOffset` | `string` (ISO 8601) |
| `Guid` | `string` (UUID format) |
| `TimeSpan` | `string` (ISO 8601 duration) |
| `Uri` | `string` |

**Why DateTime → string?**

JSON doesn't have a native Date type. .NET serializes DateTime as ISO 8601 strings:
```json
"2025-10-01T14:30:00Z"
```

You can parse in TypeScript:
```typescript
const date = new Date(dateString);
```

### Collections

| C# Type | TypeScript Type |
|---------|-----------------|
| `List<T>`, `IList<T>` | `T[]` |
| `IEnumerable<T>`, `ICollection<T>` | `T[]` |
| `T[]` | `T[]` |
| `Dictionary<TKey, TValue>` | `Record<TKey, TValue>` |
| `IDictionary<TKey, TValue>` | `Record<TKey, TValue>` |
| `HashSet<T>`, `ISet<T>` | `T[]` |

### Nullable Types

| C# Type | TypeScript Type |
|---------|-----------------|
| `int?`, `bool?`, etc. | `number \| null`, `boolean \| null` |
| `string?` (C# 8+ nullable) | `string \| null` |

## Generic Types

### Task and ValueTask

Async methods returning `Task<T>` are unwrapped:

```csharp
// C#
public async Task<Product> GetProduct(int id)
```

```typescript
// TypeScript
export async function getProduct(id: number): Promise<Product>
```

| C# Return Type | TypeScript Return Type |
|----------------|------------------------|
| `Task<T>` | `Promise<T>` |
| `ValueTask<T>` | `Promise<T>` |
| `Task` | `Promise<void>` |

### ActionResult

ASP.NET Core action results are unwrapped to their content type:

```csharp
// C#
public ActionResult<Product> GetProduct(int id)
```

```typescript
// TypeScript - unwraps to Product
export async function getProduct(id: number): Promise<Product>
```

| C# Type | TypeScript Type |
|---------|-----------------|
| `ActionResult<T>` | `T` |
| `IActionResult` | `any` |

### Custom Generic Types

```csharp
// C#
public class Result<T>
{
    public T Data { get; set; }
    public bool Success { get; set; }
}
```

```typescript
// TypeScript
export interface Result<T> {
  data: T;
  success: boolean;
}
```

## Enums

C# enums can be mapped to TypeScript enums or union types:

### TypeScript Enum (Default)

```csharp
// C#
public enum Status
{
    Active,
    Inactive,
    Pending
}
```

```typescript
// TypeScript
export enum Status {
  Active = 'Active',
  Inactive = 'Inactive',
  Pending = 'Pending'
}
```

### Union Type

Configure in `codebridge.json`:

```json
{
  "enums": {
    "format": "union"
  }
}
```

Generates:

```typescript
export type Status = 'Active' | 'Inactive' | 'Pending';
```

## Custom Type Mappings

Override default mappings in `codebridge.json`:

```json
{
  "typeMapping": {
    "System.Guid": "string",
    "System.DateTime": "Date",
    "MyApp.CustomType": "CustomInterface"
  }
}
```

### Example: DateTime as Date

```json
{
  "typeMapping": {
    "System.DateTime": "Date"
  }
}
```

Generated:

```typescript
export interface Event {
  id: number;
  startDate: Date;  // instead of string
}
```

### Example: Custom Domain Types

```csharp
// C#
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
}
```

Map to existing TypeScript type:

```json
{
  "typeMapping": {
    "MyApp.Models.Money": "Money"
  }
}
```

Provide the TypeScript type separately:

```typescript
// types/custom.ts
export interface Money {
  amount: number;
  currency: string;
}
```

## Complex Types

### Nested Objects

```csharp
// C#
public class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; }
    public List<OrderItem> Items { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class OrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
```

```typescript
// TypeScript - preserves structure
export interface Order {
  id: number;
  customer: Customer;
  items: OrderItem[];
}

export interface Customer {
  id: number;
  name: string;
}

export interface OrderItem {
  productId: number;
  quantity: number;
}
```

### Inheritance

```csharp
// C#
public class Animal
{
    public string Name { get; set; }
}

public class Dog : Animal
{
    public string Breed { get; set; }
}
```

```typescript
// TypeScript
export interface Animal {
  name: string;
}

export interface Dog extends Animal {
  breed: string;
}
```

## Edge Cases

### Optional vs Nullable

C# 8+ nullable reference types:

```csharp
// C#
public class Product
{
    public string Name { get; set; }       // Required
    public string? Description { get; set; } // Optional
}
```

```typescript
// TypeScript
export interface Product {
  name: string;
  description: string | null;
}
```

### Dynamic and Object

```csharp
// C#
public dynamic GetData()
```

```typescript
// TypeScript
export async function getData(): Promise<any>
```

### Tuple Types

```csharp
// C#
public (int Id, string Name) GetInfo()
```

```typescript
// TypeScript
export async function getInfo(): Promise<[number, string]>
```

## Best Practices

### 1. Use Specific Types

❌ Avoid:
```csharp
public object GetData()
```

✅ Prefer:
```csharp
public ProductDto GetData()
```

### 2. Use DTOs

Create Data Transfer Objects for API responses:

```csharp
public record ProductDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public decimal Price { get; init; }
}
```

### 3. Consistent Null Handling

```csharp
// Enable nullable reference types
#nullable enable

public class ProductDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
```

### 4. Document Complex Types

Use XML comments:

```csharp
/// <summary>
/// Represents money with amount and currency.
/// Amount is in the smallest currency unit (e.g., cents for USD).
/// </summary>
public class Money
{
    /// <summary>Amount in smallest unit (e.g., cents)</summary>
    public long Amount { get; set; }
    
    /// <summary>ISO 4217 currency code (e.g., "USD", "EUR")</summary>
    public string Currency { get; set; } = "USD";
}
```

## Type Validation

Validate type mappings:

```bash
codebridge validate --strict
```

This checks:
- All types have valid mappings
- No circular dependencies
- Custom mappings point to valid types

## Next Steps

- [Configuration Guide](configuration.md) - Full configuration options
- [CLI Commands](cli-commands.md) - Validation and generation commands
- [Examples](../examples/react.md) - See type mappings in action
