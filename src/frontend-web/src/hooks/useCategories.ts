import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { Category } from '@/types/categories'

export function useCategories() {
  return useQuery({
    queryKey: ['categories'],
    queryFn: () => api.get<Category[]>('/categories'),
    // Las categorías cambian muy poco: se cachean 10 min antes de considerarse stale.
    // Equivale a un objeto IMemoryCache con expiración absoluta en C#.
    staleTime: 10 * 60 * 1000,
  })
}
