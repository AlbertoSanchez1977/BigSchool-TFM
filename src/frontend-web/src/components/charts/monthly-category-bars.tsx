'use client'

import {
  Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import type { MonthlyChartPoint } from '@/types/transactions'
import type { TransactionType } from '@/types/enums'
import { formatAmount } from '@/lib/transactions/labels'

// Una gráfica por categoría (desagregada): eje X = los 12 meses, una barra por año
// dentro de cada mes (agrupadas, no apiladas). Mismo patrón visual que CategoryBars
// (grid punteado, tooltip HTML propio, leyenda manual, paleta --chart-1..4), pero con
// los ejes "girados": allí el eje X es la categoría y el año agrupa; aquí el eje X es
// el mes y el año sigue agrupando.

interface MonthlyCategoryBarsProps {
  points: MonthlyChartPoint[]
  type: TransactionType
  years: number[]
  /** Moneda base para formatear importes en el tooltip. */
  currency?: string
  /** Texto cuando no hay datos en la ventana para esta categoría. */
  emptyLabel?: string
}

const MONTH_SHORT = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic']

function kFmt(v: number): string {
  if (Math.abs(v) >= 1000) return `${+(v / 1000).toFixed(1)}k`
  return `${v}`
}

interface TooltipItem {
  dataKey: string | number
  value: number
  color: string
}

export function MonthlyCategoryBars({ points, type, years, currency = 'EUR', emptyLabel }: MonthlyCategoryBarsProps) {
  const valueOf = (p: MonthlyChartPoint) => (type === 'Income' ? p.income : p.expense)
  const hasData = points.some((p) => valueOf(p) !== 0)

  if (!hasData) {
    return (
      <div className="rounded-lg border border-border bg-card py-10 text-center text-sm text-muted-foreground">
        {emptyLabel ?? 'No hay datos en los últimos 4 años.'}
      </div>
    )
  }

  // Recharts necesita formato "ancho": una fila por mes con una clave por año.
  const chartData = MONTH_SHORT.map((label, i) => {
    const month = i + 1
    const row: Record<string, string | number> = { month: label }
    for (const y of years) {
      const point = points.find((p) => p.year === y && p.month === month)
      row[String(y)] = point ? valueOf(point) : 0
    }
    return row
  })

  function ChartTooltip(props: { active?: boolean; payload?: TooltipItem[]; label?: string }) {
    const { active, payload, label } = props
    if (!active || !payload?.length) return null
    return (
      <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md">
        <p className="mb-1 font-medium text-popover-foreground">{label}</p>
        {payload.map((item) => (
          <p
            key={item.dataKey}
            className="flex items-center justify-between gap-4 text-muted-foreground"
          >
            <span className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-full" style={{ backgroundColor: item.color }} />
              {item.dataKey}
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
      <div className="h-64 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={chartData} margin={{ top: 8, right: 4, left: -16, bottom: 0 }} barGap={2}>
            <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
            <XAxis
              dataKey="month"
              tickLine={false}
              axisLine={false}
              tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
              interval={0}
            />
            <YAxis
              tickLine={false}
              axisLine={false}
              tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
              tickFormatter={kFmt}
              width={40}
            />
            <Tooltip cursor={{ fill: 'var(--muted)' }} content={<ChartTooltip />} />
            {years.map((year, i) => (
              <Bar
                key={year}
                dataKey={String(year)}
                name={String(year)}
                fill={`var(--chart-${i + 1})`}
                radius={[3, 3, 0, 0]}
                maxBarSize={10}
              />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
        {years.map((year, i) => (
          <span key={year} className="flex items-center gap-1.5">
            <span
              className="h-2.5 w-2.5 rounded-sm"
              style={{ backgroundColor: `var(--chart-${i + 1})` }}
            />
            {year}
          </span>
        ))}
      </div>
    </div>
  )
}
