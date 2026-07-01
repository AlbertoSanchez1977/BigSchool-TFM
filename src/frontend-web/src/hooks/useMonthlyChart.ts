import { useQuery } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'

export function useMonthlyChart(year: number) {
  return useQuery({
    queryKey: ['transactions', 'monthly-chart', year],
    queryFn:  () => transactionService.monthlyChart(year),
  })
}
