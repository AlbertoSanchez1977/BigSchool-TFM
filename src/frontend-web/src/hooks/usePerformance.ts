import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import type { SellSharesDto } from '@/types/portfolios'

const performanceKey = (id: number) => ['portfolios', id, 'performance'] as const
const portfolioKey   = (id: number) => ['portfolios', id] as const

export function usePerformance(
  portfolioId: number,
  options?: { enabled?: boolean },
) {
  return useQuery({
    queryKey: performanceKey(portfolioId),
    queryFn:  () => portfolioService.performance(portfolioId),
    enabled:  options?.enabled ?? true,
  })
}

export function useSellShares(portfolioId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: SellSharesDto) => portfolioService.sellShares(portfolioId, data),
    onSuccess: () => {
      // Invalidar detalle (holdings actualizados), lista y performance
      qc.invalidateQueries({ queryKey: portfolioKey(portfolioId) })
      qc.invalidateQueries({ queryKey: ['portfolios'] })
      qc.invalidateQueries({ queryKey: performanceKey(portfolioId) })
    },
  })
}
