import type { MonthlyChartPoint } from '@/types/transactions'
import type { PortfolioListItem }  from '@/types/portfolios'

// ── Tipos ─────────────────────────────────────────────────────────────────────

export type MonthlyPoint = { month: number; income: number; expense: number }

export type CumulativePoint = { month: number; balance: number }

export type PortfolioTotals = {
  marketValue:   number
  costBasis:     number
  unrealizedPnL: number
  totalPnL:      number
  realizedPnL:   number
  returnPct:     number | null
}

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

// ── derivePortfolioTotals ─────────────────────────────────────────────────────
// Agrega los valores monetarios de todas las carteras.
// Asume que todas las carteras usan la misma moneda base del usuario.
// returnPct es null si costBasis = 0 (carteras vacías / sin coste registrado).

export function derivePortfolioTotals(portfolios: Pick<
  PortfolioListItem,
  'marketValue' | 'costBasis' | 'unrealizedPnL' | 'totalPnL' | 'realizedPnL'
>[]): PortfolioTotals {
  const sum = <K extends keyof typeof portfolios[0]>(key: K) =>
    portfolios.reduce((acc, p) => acc + (p[key] as number), 0)

  const costBasis = sum('costBasis')
  const totalPnL  = sum('totalPnL')

  return {
    marketValue:   sum('marketValue'),
    costBasis,
    unrealizedPnL: sum('unrealizedPnL'),
    totalPnL,
    realizedPnL:   sum('realizedPnL'),
    returnPct:     costBasis > 0 ? (totalPnL / costBasis) * 100 : null,
  }
}
