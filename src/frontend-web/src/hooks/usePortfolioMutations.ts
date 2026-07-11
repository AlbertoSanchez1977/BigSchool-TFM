'use client'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import type { RenamePortfolioDto } from '@/types/portfolios'
import { portfolioKeys } from '@/lib/queryKeys'

// portfolioKeys.detail(id) es la misma queryKey del detalle que useHoldings.ts usa
// para usePortfolioDetail — hay que invalidarla junto con la lista (portfolioKeys.all)
// para que ambas vistas reflejen el cambio sin esperar a un refetch manual.

export function useRenamePortfolio(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: RenamePortfolioDto) => portfolioService.rename(id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: portfolioKeys.detail(id) })
      qc.invalidateQueries({ queryKey: portfolioKeys.all })
    },
  })
}

export function useDeletePortfolio() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => portfolioService.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: portfolioKeys.all })
    },
  })
}
