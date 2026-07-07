'use client'
import { useQueries } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'
import { CATEGORIES_BY_TYPE } from '@/lib/finance/categoriesByType'
import type { MonthlyChartPoint } from '@/types/transactions'
import type { MainCategory, TransactionType } from '@/types/enums'

const WINDOW_YEARS = 4

export interface CategoryMonthlySeries {
  category: MainCategory
  points: MonthlyChartPoint[]
}

export interface UseMonthlySeriesResult {
  data: CategoryMonthlySeries[]
  isLoading: boolean
  isError: boolean
}

// Gráfica B: serie mensual apilada por categoría, filtrada solo por Tipo (Gastos/Ingresos).
// Una llamada a `monthly` por cada MainCategory del tipo (7 gasto / 4 ingreso), sobre la
// misma ventana de 4 años que la Gráfica A.
export function useMonthlySeries(
  type: TransactionType,
  referenceYear: number,
  options?: { enabled?: boolean },
): UseMonthlySeriesResult {
  const from = `${referenceYear - (WINDOW_YEARS - 1)}-01-01`
  const to = `${referenceYear}-12-31`
  const categories = CATEGORIES_BY_TYPE[type]

  const results = useQueries({
    queries: categories.map((c) => ({
      queryKey: ['transactions', 'monthly', c, type, referenceYear],
      queryFn: () => transactionService.monthly(from, to, type, c),
      enabled: options?.enabled ?? true,
    })),
  })

  const data: CategoryMonthlySeries[] = categories.map((category, i) => ({
    category,
    points: results[i].data ?? [],
  }))

  return {
    data,
    isLoading: results.some((r) => r.isLoading),
    isError: results.some((r) => r.isError),
  }
}
