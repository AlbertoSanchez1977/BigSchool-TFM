import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import type { CreatePortfolioDto } from '@/types/portfolios'

// Equivalente C#: IPortfolioService inyectado en un controller/handler.
// TanStack Query gestiona el cache de la lista; la mutación la invalida en onSuccess.

const PORTFOLIOS_KEY = ['portfolios'] as const

export function usePortfolios() {
  return useQuery({
    queryKey: PORTFOLIOS_KEY,
    queryFn:  () => portfolioService.list(),
  })
}

export function useCreatePortfolio() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreatePortfolioDto) => portfolioService.create(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PORTFOLIOS_KEY })
    },
  })
}
