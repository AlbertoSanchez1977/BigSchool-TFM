import { useQuery } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'

export function useSummary(from: string, to: string) {
  return useQuery({
    queryKey: ['transactions', 'summary', from, to],
    queryFn:  () => transactionService.summary(from, to),
    enabled:  Boolean(from && to),
  })
}
