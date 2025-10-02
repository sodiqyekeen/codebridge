# React Example

This guide shows how to integrate CodeBridge-generated SDK in a React application.

## Prerequisites

- React 18+
- TypeScript
- Axios (if using axios HTTP client)

## Setup

### 1. Install Dependencies

```bash
npm install axios
# or
yarn add axios
```

### 2. Generate SDK

In your .NET API project:

```bash
codebridge generate --output ../react-app/src/api
```

Or configure in `codebridge.json`:

```json
{
  "outputPath": "../react-app/src/api",
  "httpClient": "axios",
  "baseUrl": "https://localhost:5001"
}
```

## Basic Usage

### API Client Setup

Create an API client wrapper:

**src/lib/api-client.ts:**
```typescript
import axios from 'axios';

const apiClient = axios.create({
  baseURL: process.env.REACT_APP_API_URL || 'https://localhost:5001',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add request interceptor for authentication
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Add response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Handle unauthorized access
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default apiClient;
```

### Using Generated SDK

**src/components/ProductList.tsx:**
```typescript
import { useEffect, useState } from 'react';
import { getProducts, type Product } from '@/api';

export function ProductList() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadProducts();
  }, []);

  const loadProducts = async () => {
    try {
      setLoading(true);
      const data = await getProducts();
      setProducts(data);
    } catch (err) {
      setError('Failed to load products');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error}</div>;

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

## React Hooks

### Custom Hook for Data Fetching

**src/hooks/useProducts.ts:**
```typescript
import { useState, useEffect } from 'react';
import { getProducts, type Product } from '@/api';

export function useProducts() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    getProducts()
      .then(setProducts)
      .catch(setError)
      .finally(() => setLoading(false));
  }, []);

  const refetch = () => {
    setLoading(true);
    return getProducts()
      .then(setProducts)
      .catch(setError)
      .finally(() => setLoading(false));
  };

  return { products, loading, error, refetch };
}
```

**Usage:**
```typescript
function ProductList() {
  const { products, loading, error, refetch } = useProducts();

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <div>
      <button onClick={refetch}>Refresh</button>
      <ul>
        {products.map(p => <li key={p.id}>{p.name}</li>)}
      </ul>
    </div>
  );
}
```

### Generic API Hook

**src/hooks/useApi.ts:**
```typescript
import { useState, useCallback } from 'react';

interface UseApiState<T> {
  data: T | null;
  loading: boolean;
  error: Error | null;
}

export function useApi<T>(apiFunc: () => Promise<T>) {
  const [state, setState] = useState<UseApiState<T>>({
    data: null,
    loading: false,
    error: null,
  });

  const execute = useCallback(async () => {
    setState({ data: null, loading: true, error: null });
    try {
      const data = await apiFunc();
      setState({ data, loading: false, error: null });
      return data;
    } catch (error) {
      setState({ data: null, loading: false, error: error as Error });
      throw error;
    }
  }, [apiFunc]);

  return { ...state, execute };
}
```

**Usage:**
```typescript
function ProductDetails({ id }: { id: number }) {
  const { data: product, loading, error, execute } = useApi(() => getProduct(id));

  useEffect(() => {
    execute();
  }, [id, execute]);

  // Render logic...
}
```

## React Query Integration

For advanced data fetching with caching:

**src/hooks/useProductsQuery.ts:**
```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getProducts, createProduct, deleteProduct, type CreateProductRequest } from '@/api';

export function useProductsQuery() {
  return useQuery({
    queryKey: ['products'],
    queryFn: getProducts,
  });
}

export function useCreateProduct() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateProductRequest) => createProduct(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
    },
  });
}

export function useDeleteProduct() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => deleteProduct(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
    },
  });
}
```

**Usage:**
```typescript
function ProductList() {
  const { data: products, isLoading, error } = useProductsQuery();
  const createMutation = useCreateProduct();
  const deleteMutation = useDeleteProduct();

  const handleCreate = () => {
    createMutation.mutate({
      name: 'New Product',
      price: 29.99,
    });
  };

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <div>
      <button onClick={handleCreate}>Create Product</button>
      <ul>
        {products?.map((product) => (
          <li key={product.id}>
            {product.name}
            <button onClick={() => deleteMutation.mutate(product.id)}>
              Delete
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

## Form Handling

**src/components/ProductForm.tsx:**
```typescript
import { useState } from 'react';
import { createProduct, type CreateProductRequest } from '@/api';

export function ProductForm({ onSuccess }: { onSuccess: () => void }) {
  const [formData, setFormData] = useState<CreateProductRequest>({
    name: '',
    price: 0,
  });
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);

    try {
      await createProduct(formData);
      onSuccess();
      setFormData({ name: '', price: 0 });
    } catch (error) {
      console.error('Failed to create product:', error);
    } finally {
      setSubmitting(false);
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
      <button type="submit" disabled={submitting}>
        {submitting ? 'Creating...' : 'Create Product'}
      </button>
    </form>
  );
}
```

## Error Boundaries

**src/components/ErrorBoundary.tsx:**
```typescript
import { Component, ReactNode } from 'react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback || (
        <div>
          <h2>Something went wrong</h2>
          <p>{this.state.error?.message}</p>
        </div>
      );
    }

    return this.props.children;
  }
}
```

## Environment Configuration

**.env.development:**
```bash
REACT_APP_API_URL=https://localhost:5001
```

**.env.production:**
```bash
REACT_APP_API_URL=https://api.example.com
```

Update generated SDK base URL:

**src/api/config.ts:**
```typescript
export const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:5001';
```

## Complete Example

**src/App.tsx:**
```typescript
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProductList } from './components/ProductList';
import { ErrorBoundary } from './components/ErrorBoundary';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 3,
      staleTime: 5 * 60 * 1000, // 5 minutes
    },
  },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ErrorBoundary>
        <div className="App">
          <ProductList />
        </div>
      </ErrorBoundary>
    </QueryClientProvider>
  );
}
```

## Next Steps

- [Next.js Example](nextjs.md) - Server-side rendering with Next.js
- [Vue Example](vue.md) - Using CodeBridge with Vue 3
- [Configuration Guide](../guides/configuration.md) - Customize SDK generation
