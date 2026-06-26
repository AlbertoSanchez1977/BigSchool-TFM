import { useQuery } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'
import type { TransactionFilters } from '@/types/transactions'

// TanStack Query es el equivalente a tener un IMemoryCache + IHttpClientFactory en C#:
// gestiona el ciclo de vida de la petición (loading/error/success), el cacheo
// (staleTime) y la revalidación automática.
//
// queryKey: identifica unívocamente la query en el cache.  Incluir los filtros
// garantiza que cambiar mes/año invalide la entrada anterior y dispare un nuevo fetch.
// Equivale a construir una clave de caché basada en los parámetros de la consulta.

export function useTransactions(filters: TransactionFilters) {
  return useQuery({
    queryKey: ['transactions', filters],
    queryFn:  () => transactionService.list(filters),
  })
}
