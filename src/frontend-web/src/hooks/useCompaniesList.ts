'use client'
import { useQuery } from '@tanstack/react-query'
import { companyService } from '@/services/companyService'
import { companyKeys } from '@/lib/queryKeys'

// GET /companies?page&pageSize — listado paginado de /market (distinto de useCompanies,
// que trae hasta 100 sin paginar para el combobox de "añadir holding").
export function useCompaniesList({ page, pageSize }: { page: number; pageSize: number }) {
  return useQuery({
    queryKey: companyKeys.list(page, pageSize),
    queryFn:  () => companyService.listPaged({ page, pageSize }),
  })
}
