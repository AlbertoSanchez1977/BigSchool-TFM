import type { MonthlyChartPoint } from '@/types/transactions'

// ── Tipos ─────────────────────────────────────────────────────────────────────

export type MonthlyPoint = { month: number; income: number; expense: number }

export type CumulativePoint = { month: number; balance: number }

// ── deriveMonthlyPoints ───────────────────────────────────────────────────────
// Expande los puntos del backend (solo meses con transacciones) a los 12 meses
// del año, rellenando los meses sin datos con income=0, expense=0.

export function deriveMonthlyPoints(points: MonthlyChartPoint[]): MonthlyPoint[] {
  return Array.from({ length: 12 }, (_, i) => {
    const month = i + 1
    const p = points.find(x => x.month === month)
    return { month, income: p?.income ?? 0, expense: p?.expense ?? 0 }
  })
}

// ── deriveCumulativeBalance ───────────────────────────────────────────────────
// Calcula el balance acumulado mes a mes (income - expense) a partir de los
// puntos del monthly-chart. Los meses sin datos propagan el acumulado anterior.

export function deriveCumulativeBalance(points: MonthlyChartPoint[]): CumulativePoint[] {
  let running = 0
  return Array.from({ length: 12 }, (_, i) => {
    const month = i + 1
    const p = points.find(x => x.month === month)
    if (p) running += p.income - p.expense
    return { month, balance: running }
  })
}

// ── deriveSavingsRate ─────────────────────────────────────────────────────────
// Tasa de ahorro = (income - expense) / income * 100.
// Devuelve null si income <= 0 (no hay base sobre la que calcular).

export function deriveSavingsRate(income: number, expense: number): number | null {
  if (income <= 0) return null
  return ((income - expense) / income) * 100
}
