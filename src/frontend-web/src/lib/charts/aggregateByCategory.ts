import type { Transaction } from '@/types/transactions'
import type { MainCategory, TransactionType } from '@/types/enums'

// ── Agregación por categoría y año (hueco #1 de la spec) ───────────────────────
//
// El Backend agrega por mes y por tipo, NO por categoría. Esta función pura hace
// esa agregación en el cliente a partir de la lista de transacciones, para la
// pestaña de gráficas de Gastos/Ingresos.
//
// Es deliberadamente pura (sin hooks, sin fetch): recibe los datos y devuelve la
// estructura ya lista para Recharts. Así es trivial de testear (TDD) y reutilizable.

export interface CategoryYearTotals {
  category: MainCategory
  /** año → total sumado (baseAmount). Incluye los 4 años de la ventana, 0 donde no hay datos. */
  totals: Record<number, number>
}

export interface CategoryAggregation {
  /** Ventana de 4 años en orden ascendente, p. ej. [2023, 2024, 2025, 2026]. */
  years: number[]
  /** Una fila por categoría con datos, ordenadas por total acumulado descendente. */
  rows: CategoryYearTotals[]
}

const WINDOW_YEARS = 4

// El año viaja en "YYYY-MM-DD"; lo extraemos por slice para evitar que `new Date`
// reinterprete la zona horaria (que podría desplazar el día y, en bordes, el año).
function yearOf(isoDate: string): number {
  return Number(isoDate.slice(0, 4))
}

// Redondeo a 2 decimales: sumar floats (0.1 + 0.2) arrastra ruido binario; al ser
// importes monetarios, 2 decimales es la precisión correcta.
function round2(n: number): number {
  return Math.round(n * 100) / 100
}

/**
 * Agrega transacciones por categoría y año dentro de la ventana de los últimos 4 años.
 *
 * @param transactions Lista de transacciones (cualquier tipo/categoría).
 * @param type Solo se agregan las del tipo pedido (gasto e ingreso usan categorías disjuntas).
 * @param referenceYear Año más reciente de la ventana. Por defecto el actual; explícito en tests.
 */
export function aggregateByCategory(
  transactions: Transaction[],
  type: TransactionType,
  referenceYear: number = new Date().getFullYear(),
): CategoryAggregation {
  // Ventana ascendente: [ref-3, ref-2, ref-1, ref].
  const years = Array.from(
    { length: WINDOW_YEARS },
    (_, i) => referenceYear - (WINDOW_YEARS - 1 - i),
  )
  const minYear = years[0]
  const maxYear = years[years.length - 1]

  // Acumulador: categoría → (año → total). Solo creamos entrada cuando hay datos.
  const byCategory = new Map<MainCategory, Record<number, number>>()

  for (const t of transactions) {
    if (t.type !== type) continue
    const y = yearOf(t.transactionDate)
    if (y < minYear || y > maxYear) continue

    let totals = byCategory.get(t.idMainCategory)
    if (!totals) {
      // Inicializamos los 4 años a 0 para que toda fila tenga la ventana completa.
      totals = Object.fromEntries(years.map((yr) => [yr, 0]))
      byCategory.set(t.idMainCategory, totals)
    }
    totals[y] = round2(totals[y] + t.baseAmount)
  }

  const rows: CategoryYearTotals[] = [...byCategory.entries()]
    .map(([category, totals]) => ({ category, totals }))
    .sort((a, b) => {
      const totalA = years.reduce((sum, yr) => sum + a.totals[yr], 0)
      const totalB = years.reduce((sum, yr) => sum + b.totals[yr], 0)
      if (totalB !== totalA) return totalB - totalA          // total descendente
      return a.category.localeCompare(b.category)            // desempate estable
    })

  return { years, rows }
}
