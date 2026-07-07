'use client'

import {
  Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import type { CategoryMonthlySeries } from '@/hooks/useMonthlySeries'
import { MAIN_CATEGORY_LABEL, formatAmount } from '@/lib/transactions/labels'
import type { TransactionType } from '@/types/enums'

// Barras apiladas por mes: una pila = un mes, segmentos = categorías del tipo elegido.
// Misma convención visual que CategoryBars (grid punteado, tooltip HTML propio, paleta
// --chart-1..4 cíclica si hay más de 4 categorías — Gastos tiene 7).

interface MonthlyStackedBarsProps {
  data: CategoryMonthlySeries[]
  type: TransactionType
  years: number[]
  currency?: string
  emptyLabel?: string
}

const MONTH_SHORT = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic']

function kFmt(v: number): string {
  if (Math.abs(v) >= 1000) return `${+(v / 1000).toFixed(1)}k`
  return `${v}`
}

interface TooltipItem {
  dataKey: string | number
  value: number
  color: string
}

export function MonthlyStackedBars({ data, type, years, currency = 'EUR', emptyLabel }: MonthlyStackedBarsProps) {
  const hasAnyPoint = data.some((series) => series.points.length > 0)

  if (!hasAnyPoint) {
    return (
      <div className="rounded-lg border border-border bg-card py-16 text-center text-sm text-muted-foreground">
        {emptyLabel ?? 'No hay datos en los últimos 4 años.'}
      </div>
    )
  }

  // Recharts necesita formato "ancho": una fila por (año, mes) con una clave por categoría.
  const chartData = years.flatMap((year) =>
    MONTH_SHORT.map((monthLabel, i) => {
      const month = i + 1
      const row: Record<string, string | number> = { label: `${monthLabel} ${String(year).slice(2)}` }
      for (const series of data) {
        const point = series.points.find((p) => p.year === year && p.month === month)
        row[series.category] = point ? (type === 'Income' ? point.income : point.expense) : 0
      }
      return row
    }),
  )

  function ChartTooltip(props: { active?: boolean; payload?: TooltipItem[]; label?: string }) {
    const { active, payload, label } = props
    if (!active || !payload?.length) return null
    const nonZero = payload.filter((item) => item.value !== 0)
    if (nonZero.length === 0) return null
    return (
      <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md">
        <p className="mb-1 font-medium text-popover-foreground">{label}</p>
        {nonZero.map((item) => (
          <p
            key={item.dataKey}
            className="flex items-center justify-between gap-4 text-muted-foreground"
          >
            <span className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-full" style={{ backgroundColor: item.color }} />
              {MAIN_CATEGORY_LABEL[item.dataKey as keyof typeof MAIN_CATEGORY_LABEL]}
            </span>
            <span className="font-medium text-popover-foreground">
              {formatAmount(item.value, currency)}
            </span>
          </p>
        ))}
      </div>
    )
  }

  return (
    <div className="rounded-lg border border-border bg-card p-4">
      <div className="h-80 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={chartData} margin={{ top: 8, right: 4, left: -16, bottom: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
            <XAxis
              dataKey="label"
              tickLine={false}
              axisLine={false}
              tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
              interval="preserveStartEnd"
            />
            <YAxis
              tickLine={false}
              axisLine={false}
              tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
              tickFormatter={kFmt}
            />
            <Tooltip cursor={{ fill: 'var(--muted)' }} content={<ChartTooltip />} />
            {data.map((series, i) => (
              <Bar
                key={series.category}
                dataKey={series.category}
                name={MAIN_CATEGORY_LABEL[series.category]}
                stackId="a"
                fill={`var(--chart-${(i % 4) + 1})`}
                maxBarSize={20}
              />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
        {data.map((series, i) => (
          <span key={series.category} className="flex items-center gap-1.5">
            <span
              className="h-2.5 w-2.5 rounded-sm"
              style={{ backgroundColor: `var(--chart-${(i % 4) + 1})` }}
            />
            {MAIN_CATEGORY_LABEL[series.category]}
          </span>
        ))}
      </div>
    </div>
  )
}
