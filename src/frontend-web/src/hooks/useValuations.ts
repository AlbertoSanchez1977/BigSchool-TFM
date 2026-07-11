'use client'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { companyService } from '@/services/companyService'
import type { CreateValuationDto } from '@/types/companies'
import type { ValuationPeriod } from '@/types/enums'
import { valuationKeys } from '@/lib/queryKeys'

// GET /companies/{id}/valuations?page&pageSize — listado paginado de valoraciones.
export function useValuations(companyId: number, page: number, pageSize: number) {
  return useQuery({
    queryKey: valuationKeys.list(companyId, page, pageSize),
    queryFn:  () => companyService.listValuations(companyId, page, pageSize),
  })
}

// POST /companies/{id}/valuations — invalida el listado paginado y la serie: una valoración
// nueva cambia tanto la tabla como el gráfico (y su summary min/max/last/changePct).
export function useCreateValuation(companyId: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateValuationDto) => companyService.createValuation(companyId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: valuationKeys.all(companyId) })
      qc.invalidateQueries({ queryKey: valuationKeys.seriesAll(companyId) })
    },
  })
}

// GET /companies/{id}/valuations/series?period= — serie de cotización + summary.
export function useValuationSeries(companyId: number, period: ValuationPeriod) {
  return useQuery({
    queryKey: valuationKeys.series(companyId, period),
    queryFn:  () => companyService.valuationSeries(companyId, period),
  })
}
