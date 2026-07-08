'use client'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { companyService } from '@/services/companyService'
import type { CreateCompanyDto } from '@/types/companies'
import { companyKeys } from '@/lib/queryKeys'

// POST /companies — invalida el prefijo companyKeys.all entero: el listado paginado de
// /market (companyKeys.list) y el combobox de "añadir holding" (companyKeys.all,
// useCompanies en useCompanies.ts) deben ver la empresa nueva sin esperar a un refetch manual.
export function useCreateCompany() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateCompanyDto) => companyService.create(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: companyKeys.all })
    },
  })
}
