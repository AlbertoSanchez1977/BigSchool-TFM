import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import type { CreatePortfolioDto } from '@/types/portfolios'

// Equivalente C#: IPortfolioService inyectado en un controller/handler.
// TanStack Query gestiona el cache de la lista; la mutación la invalida en onSuccess.

const PORTFOLIOS_KEY = ['portfolios'] as const

export function usePortfolios({ page, pageSize }: { page: number; pageSize: number }) {
  return useQuery({
    queryKey: [...PORTFOLIOS_KEY, page, pageSize],
    queryFn:  () => portfolioService.list({ page, pageSize }),
  })
}

export function useCreatePortfolio() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreatePortfolioDto) => portfolioService.create(data),
    onSuccess: () => {
      // Prefijo ['portfolios'] invalida todas las páginas cacheadas (['portfolios', page, pageSize]).
      qc.invalidateQueries({ queryKey: PORTFOLIOS_KEY })
    },
  })
}
