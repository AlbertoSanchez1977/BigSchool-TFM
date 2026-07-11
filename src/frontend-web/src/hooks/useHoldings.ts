import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { holdingsService } from '@/services/holdingsService'
import type { AddHoldingDto, UpdateHoldingNotesDto } from '@/types/portfolios'
import { portfolioKeys } from '@/lib/queryKeys'

// portfolioKeys.detail(id): cada cartera tiene su propia entrada en caché. Al mutar
// (añadir/editar/borrar) se invalida esta key para que la lista de holdings se
// refresque automáticamente.

export function usePortfolioDetail(id: number) {
  return useQuery({
    queryKey: portfolioKeys.detail(id),
    queryFn:  () => holdingsService.getPortfolio(id),
  })
}

export function useAddHolding(portfolioId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: AddHoldingDto) => holdingsService.addHolding(portfolioId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: portfolioKeys.detail(portfolioId) })
    },
  })
}

export function useUpdateHoldingNotes(portfolioId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ holdingId, notes }: { holdingId: number; notes: string | null }) =>
      holdingsService.updateNotes(portfolioId, holdingId, { notes }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: portfolioKeys.detail(portfolioId) })
    },
  })
}

export function useDeleteHolding(portfolioId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (holdingId: number) => holdingsService.deleteHolding(portfolioId, holdingId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: portfolioKeys.detail(portfolioId) })
      // Invalida también la lista de carteras para que el marketValue se refresque
      qc.invalidateQueries({ queryKey: portfolioKeys.all })
    },
  })
}
