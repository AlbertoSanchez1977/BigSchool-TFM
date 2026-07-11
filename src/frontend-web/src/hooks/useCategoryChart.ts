'use client'
import { useMemo } from 'react'
import { useQueries } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'
import type { CategoryAggregation, CategoryYearTotals } from '@/lib/charts/aggregateByCategory'
import type { MainCategory, TransactionType } from '@/types/enums'

// ── Seam (costura) entre la FUENTE de datos y la GRÁFICA ───────────────────────
//
// La gráfica (CategoryBars) depende SOLO de `CategoryAggregation`; ni ella ni la
// página se tocan al cambiar este hook. Antes se agregaba en cliente sobre
// `useTransactions` (pageSize=5000, topado en 100 por el backend → nunca agregaba
// los 4 años completos = bug real). Ahora se tira de `GET /transactions/by-category`
// (agrega en SQL): 4 llamadas, una por año de la ventana, sin límite de filas.
// `aggregateByCategory` (función pura) se conserva para tests/fallback offline.
// ───────────────────────────────────────────────────────────────────────────────

const WINDOW_YEARS = 4

export interface UseCategoryChartResult {
  data: CategoryAggregation
  isLoading: boolean
  isError: boolean
}

export function useCategoryChart(
  type: TransactionType,
  referenceYear: number = new Date().getFullYear(),
  options?: { enabled?: boolean },
): UseCategoryChartResult {
  const years = Array.from({ length: WINDOW_YEARS }, (_, i) => referenceYear - (WINDOW_YEARS - 1 - i))

  const results = useQueries({
    queries: years.map((y) => ({
      queryKey: ['transactions', 'by-category', y, type],
      queryFn: () => transactionService.byCategory(`${y}-01-01`, `${y}-12-31`, type),
      enabled: options?.enabled ?? true,
    })),
  })

  const data = useMemo<CategoryAggregation>(() => {
    const map = new Map<MainCategory, Record<number, number>>()
    years.forEach((y, i) => {
      for (const ct of results[i].data ?? []) {
        const totals = map.get(ct.mainCategory) ?? Object.fromEntries(years.map((yr) => [yr, 0]))
        totals[y] = ct.total
        map.set(ct.mainCategory, totals)
      }
    })
    const rows: CategoryYearTotals[] = [...map.entries()]
      .map(([category, totals]) => ({ category, totals }))
      .sort((a, b) => {
        const ta = years.reduce((s, yr) => s + a.totals[yr], 0)
        const tb = years.reduce((s, yr) => s + b.totals[yr], 0)
        return tb !== ta ? tb - ta : a.category.localeCompare(b.category)
      })
    return { years, rows }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [results.map((r) => r.data), type, referenceYear])

  return {
    data,
    isLoading: results.some((r) => r.isLoading),
    isError: results.some((r) => r.isError),
  }
}
