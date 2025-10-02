# Next.js Example

This guide shows how to use CodeBridge-generated SDK in a Next.js application with App Router and Server Components.

## Prerequisites

- Next.js 14+
- TypeScript
- App Router enabled

## Setup

### 1. Generate SDK

Configure CodeBridge for Next.js:

**codebridge.json:**
```json
{
  "outputPath": "./src/lib/api",
  "httpClient": "fetch",
  "baseUrl": "https://api.example.com",
  "generateInterfaces": true
}
```

Generate:

```bash
codebridge generate
```

### 2. Environment Variables

**.env.local:**
```bash
NEXT_PUBLIC_API_URL=https://localhost:5001
API_URL=https://localhost:5001
```

**.env.production:**
```bash
NEXT_PUBLIC_API_URL=https://api.example.com
API_URL=https://api.example.com
```

## Server Components (Recommended)

### Fetching Data

**app/products/page.tsx:**
```typescript
import { getProducts } from '@/lib/api';

export default async function ProductsPage() {
  const products = await getProducts();

  return (
    <div>
      <h1>Products</h1>
      <ul>
        {products.map((product) => (
          <li key={product.id}>
            {product.name} - ${product.price}
          </li>
        ))}
      </ul>
    </div>
  );
}
```

### With Loading State

**app/products/loading.tsx:**
```typescript
export default function Loading() {
  return <div>Loading products...</div>;
}
```

### With Error Handling

**app/products/error.tsx:**
```typescript
'use client';

export default function Error({
  error,
  reset,
}: {
  error: Error;
  reset: () => void;
}) {
  return (
    <div>
      <h2>Something went wrong!</h2>
      <p>{error.message}</p>
      <button onClick={reset}>Try again</button>
    </div>
  );
}
```

### Dynamic Routes

**app/products/[id]/page.tsx:**
```typescript
import { getProduct } from '@/lib/api';
import { notFound } from 'next/navigation';

interface Props {
  params: { id: string };
}

export default async function ProductPage({ params }: Props) {
  try {
    const product = await getProduct(parseInt(params.id));

    return (
      <div>
        <h1>{product.name}</h1>
        <p>Price: ${product.price}</p>
      </div>
    );
  } catch (error) {
    notFound();
  }
}
```

## Client Components

### Interactive Forms

**app/products/new/page.tsx:**
```typescript
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { createProduct, type CreateProductRequest } from '@/lib/api';

export default function NewProductPage() {
  const router = useRouter();
  const [formData, setFormData] = useState<CreateProductRequest>({
    name: '',
    price: 0,
  });
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);

    try {
      await createProduct(formData);
      router.push('/products');
      router.refresh(); // Revalidate server components
    } catch (error) {
      console.error('Failed to create product:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <input
        type="text"
        value={formData.name}
        onChange={(e) => setFormData({ ...formData, name: e.target.value })}
        placeholder="Product name"
        required
      />
      <input
        type="number"
        value={formData.price}
        onChange={(e) => setFormData({ ...formData, price: parseFloat(e.target.value) })}
        placeholder="Price"
        required
      />
      <button type="submit" disabled={loading}>
        {loading ? 'Creating...' : 'Create Product'}
      </button>
    </form>
  );
}
```

## Server Actions

Next.js 14+ supports Server Actions for mutations:

**app/actions/products.ts:**
```typescript
'use server';

import { revalidatePath } from 'next/cache';
import { createProduct, deleteProduct, type CreateProductRequest } from '@/lib/api';

export async function createProductAction(formData: FormData) {
  const request: CreateProductRequest = {
    name: formData.get('name') as string,
    price: parseFloat(formData.get('price') as string),
  };

  await createProduct(request);
  revalidatePath('/products');
}

export async function deleteProductAction(id: number) {
  await deleteProduct(id);
  revalidatePath('/products');
}
```

**app/products/new/page.tsx:**
```typescript
import { createProductAction } from '@/app/actions/products';

export default function NewProductPage() {
  return (
    <form action={createProductAction}>
      <input name="name" type="text" placeholder="Product name" required />
      <input name="price" type="number" placeholder="Price" required />
      <button type="submit">Create Product</button>
    </form>
  );
}
```

## API Routes

### Route Handlers

**app/api/products/route.ts:**
```typescript
import { NextRequest, NextResponse } from 'next/server';
import { getProducts } from '@/lib/api';

export async function GET(request: NextRequest) {
  try {
    const products = await getProducts();
    return NextResponse.json(products);
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to fetch products' },
      { status: 500 }
    );
  }
}
```

**app/api/products/[id]/route.ts:**
```typescript
import { NextRequest, NextResponse } from 'next/server';
import { getProduct, deleteProduct } from '@/lib/api';

interface Context {
  params: { id: string };
}

export async function GET(request: NextRequest, { params }: Context) {
  try {
    const product = await getProduct(parseInt(params.id));
    return NextResponse.json(product);
  } catch (error) {
    return NextResponse.json(
      { error: 'Product not found' },
      { status: 404 }
    );
  }
}

export async function DELETE(request: NextRequest, { params }: Context) {
  try {
    await deleteProduct(parseInt(params.id));
    return NextResponse.json({ success: true });
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to delete product' },
      { status: 500 }
    );
  }
}
```

## Data Revalidation

### Time-based Revalidation

**app/products/page.tsx:**
```typescript
import { getProducts } from '@/lib/api';

export const revalidate = 60; // Revalidate every 60 seconds

export default async function ProductsPage() {
  const products = await getProducts();
  return (/* JSX */);
}
```

### On-demand Revalidation

```typescript
import { revalidatePath, revalidateTag } from 'next/cache';

// Revalidate specific path
revalidatePath('/products');

// Revalidate by tag
revalidateTag('products');
```

## Streaming and Suspense

**app/products/page.tsx:**
```typescript
import { Suspense } from 'react';
import { ProductList } from './ProductList';
import { ProductListSkeleton } from './ProductListSkeleton';

export default function ProductsPage() {
  return (
    <div>
      <h1>Products</h1>
      <Suspense fallback={<ProductListSkeleton />}>
        <ProductList />
      </Suspense>
    </div>
  );
}
```

**app/products/ProductList.tsx:**
```typescript
import { getProducts } from '@/lib/api';

export async function ProductList() {
  const products = await getProducts();

  return (
    <ul>
      {products.map((product) => (
        <li key={product.id}>{product.name}</li>
      ))}
    </ul>
  );
}
```

## Middleware for Authentication

**middleware.ts:**
```typescript
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const token = request.cookies.get('authToken')?.value;

  if (!token && request.nextUrl.pathname.startsWith('/products')) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  // Add token to headers for API calls
  const requestHeaders = new Headers(request.headers);
  if (token) {
    requestHeaders.set('Authorization', `Bearer ${token}`);
  }

  return NextResponse.next({
    request: {
      headers: requestHeaders,
    },
  });
}

export const config = {
  matcher: '/products/:path*',
};
```

## API Client Configuration

**src/lib/api-client.ts:**
```typescript
const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://localhost:5001';

export async function apiRequest<T>(
  endpoint: string,
  options?: RequestInit
): Promise<T> {
  const response = await fetch(`${API_URL}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    throw new Error(`API error: ${response.status}`);
  }

  return response.json();
}
```

## TypeScript Configuration

**tsconfig.json:**
```json
{
  "compilerOptions": {
    "target": "ES2017",
    "lib": ["dom", "dom.iterable", "esnext"],
    "allowJs": true,
    "skipLibCheck": true,
    "strict": true,
    "forceConsistentCasingInFileNames": true,
    "noEmit": true,
    "esModuleInterop": true,
    "module": "esnext",
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "jsx": "preserve",
    "incremental": true,
    "paths": {
      "@/*": ["./src/*"]
    }
  }
}
```

## Package.json Scripts

**package.json:**
```json
{
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "sdk:generate": "cd ../api && codebridge generate",
    "sdk:watch": "cd ../api && codebridge watch"
  }
}
```

## Complete Example

**app/products/page.tsx:**
```typescript
import { Suspense } from 'react';
import { getProducts } from '@/lib/api';
import Link from 'next/link';

export const revalidate = 60;

async function ProductList() {
  const products = await getProducts();

  return (
    <div className="grid grid-cols-3 gap-4">
      {products.map((product) => (
        <Link
          key={product.id}
          href={`/products/${product.id}`}
          className="border p-4 rounded hover:shadow-lg"
        >
          <h3 className="font-bold">{product.name}</h3>
          <p className="text-gray-600">${product.price}</p>
        </Link>
      ))}
    </div>
  );
}

function ProductListSkeleton() {
  return (
    <div className="grid grid-cols-3 gap-4">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="border p-4 rounded animate-pulse">
          <div className="h-6 bg-gray-200 rounded mb-2" />
          <div className="h-4 bg-gray-200 rounded w-20" />
        </div>
      ))}
    </div>
  );
}

export default function ProductsPage() {
  return (
    <div className="container mx-auto p-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold">Products</h1>
        <Link
          href="/products/new"
          className="bg-blue-500 text-white px-4 py-2 rounded"
        >
          Add Product
        </Link>
      </div>
      <Suspense fallback={<ProductListSkeleton />}>
        <ProductList />
      </Suspense>
    </div>
  );
}
```

## Next Steps

- [React Example](react.md) - Client-side React patterns
- [Configuration Guide](../guides/configuration.md) - Customize SDK generation
- [Type Mapping](../guides/type-mapping.md) - Type conversion details
