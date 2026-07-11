import { useQuery } from '@tanstack/react-query'
import { holdingsService } from '@/services/holdingsService'
import { companyKeys } from '@/lib/queryKeys'

// Las empresas no cambian con frecuencia → staleTime alto para no repetir la
// petición cada vez que se abre el modal de añadir holding.
export function useCompanies() {
  return useQuery({
    queryKey: companyKeys.all,
    queryFn:  () => holdingsService.listCompanies(),
    staleTime: 10 * 60 * 1000,
  })
}
