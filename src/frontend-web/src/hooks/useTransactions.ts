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

// `options.enabled` permite diferir el fetch (p. ej. la pestaña de gráficas no debe
// pedir 4 años de transacciones hasta que el usuario la abre). Por defecto, activo.
export function useTransactions(
  filters: TransactionFilters,
  options?: { enabled?: boolean },
) {
  return useQuery({
    queryKey: ['transactions', filters],
    queryFn:  () => transactionService.list(filters),
    enabled:  options?.enabled ?? true,
  })
}
