import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import type { CreatePortfolioDto } from '@/types/portfolios'
import { portfolioKeys } from '@/lib/queryKeys'

// Equivalente C#: IPortfolioService inyectado en un controller/handler.
// TanStack Query gestiona el cache de la lista; la mutación la invalida en onSuccess.

export function usePortfolios({ page, pageSize }: { page: number; pageSize: number }) {
  return useQuery({
    queryKey: portfolioKeys.list(page, pageSize),
    queryFn:  () => portfolioService.list({ page, pageSize }),
  })
}

export function useCreatePortfolio() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreatePortfolioDto) => portfolioService.create(data),
    onSuccess: () => {
      // Prefijo portfolioKeys.all invalida todas las páginas cacheadas (portfolioKeys.list(page,pageSize)).
      qc.invalidateQueries({ queryKey: portfolioKeys.all })
    },
  })
}
