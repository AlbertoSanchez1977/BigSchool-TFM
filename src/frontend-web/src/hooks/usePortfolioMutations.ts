'use client'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import type { RenamePortfolioDto } from '@/types/portfolios'

// Misma queryKey del detalle que useHoldings.ts (['portfolios', id]) — hay que
// invalidarla junto con la lista (['portfolios']) para que ambas vistas reflejen
// el cambio sin esperar a un refetch manual.
const portfolioKey = (id: number) => ['portfolios', id] as const
const PORTFOLIOS_KEY = ['portfolios'] as const

export function useRenamePortfolio(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: RenamePortfolioDto) => portfolioService.rename(id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: portfolioKey(id) })
      qc.invalidateQueries({ queryKey: PORTFOLIOS_KEY })
    },
  })
}

export function useDeletePortfolio() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => portfolioService.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PORTFOLIOS_KEY })
    },
  })
}
