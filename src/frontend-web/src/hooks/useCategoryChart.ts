import { useMemo } from 'react'
import { useTransactions } from '@/hooks/useTransactions'
import { aggregateByCategory, type CategoryAggregation } from '@/lib/charts/aggregateByCategory'
import type { TransactionType } from '@/types/enums'

// ── Seam (costura) entre la FUENTE de datos y la GRÁFICA ───────────────────────
//
// La gráfica (CategoryBars) depende SOLO de `CategoryAggregation`. Este hook es el
// único punto que decide de dónde sale esa agregación. Hoy se calcula en cliente
// (hueco #1 de la spec: el backend no agrega por categoría); el día que exista el
// endpoint, basta cambiar el cuerpo de este hook — ni la página ni la gráfica se tocan.
//
// ───────────────────────────────────────────────────────────────────────────────
// CONTRATO FUTURO (deuda técnica — backend en otra sesión)
// Gemelo de `GET /transactions/monthly-chart?year=` → `MonthlyChartPointDto[]`.
//
//   GET /transactions/category-chart?from=YYYY-MM-DD&to=YYYY-MM-DD&type=Expense
//   → ApiResponse<CategoryChartPointDto[]>
//     CategoryChartPointDto { category: MainCategory; year: int; total: decimal }
//
// Migración (sin tocar UI):
//   1. transactionService.categoryChart(from, to, type) → CategoryChartPointDto[]
//   2. mapear esos puntos a CategoryAggregation { years, rows } (misma forma que
//      devuelve aggregateByCategory) y devolverlo desde aquí.
//   3. `aggregateByCategory` (función pura) permanece para tests / fallback offline.
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
  // Ventana de 4 años → rango de fechas para la query (de 1-ene del más antiguo a
  // 31-dic del más reciente). pageSize amplio: queremos TODAS las transacciones del
  // periodo en una página, porque la agregación necesita el conjunto completo.
  const fromYear = referenceYear - (WINDOW_YEARS - 1)
  const from = `${fromYear}-01-01`
  const to   = `${referenceYear}-12-31`

  const query = useTransactions(
    { from, to, page: 1, pageSize: 5000 },
    { enabled: options?.enabled ?? true },
  )

  // Agregación en cliente. useMemo evita recalcular en cada render si no cambian
  // ni los datos ni el tipo/año seleccionados.
  const data = useMemo(
    () => aggregateByCategory(query.data?.items ?? [], type, referenceYear),
    [query.data, type, referenceYear],
  )

  return { data, isLoading: query.isLoading, isError: query.isError }
}
