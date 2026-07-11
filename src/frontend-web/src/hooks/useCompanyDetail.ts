'use client'
import { useQuery } from '@tanstack/react-query'
import { companyService } from '@/services/companyService'
import { companyKeys } from '@/lib/queryKeys'

// GET /companies/{id}
export function useCompanyDetail(id: number) {
  return useQuery({
    queryKey: companyKeys.detail(id),
    queryFn:  () => companyService.getById(id),
  })
}
