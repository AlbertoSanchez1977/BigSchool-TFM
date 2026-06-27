'use client'

import {
  Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import type { CategoryAggregation } from '@/lib/charts/aggregateByCategory'
import { MAIN_CATEGORY_LABEL, formatAmount } from '@/lib/transactions/labels'

// Barras agrupadas por categoría (eje X) con una barra por año dentro de cada grupo.
// Los colores rotan por año con la paleta --chart-1..4 (ver design doc §3).
// La gráfica es "tonta": recibe la agregación ya calculada (función pura testeada)
// y solo la pinta. Toda la lógica vive en aggregateByCategory.
//
// Convenciones de maquetación alineadas con el chart de v0
// (components/charts/income-expense-chart.tsx): grid punteado, ejes sin línea,
// tooltip HTML propio, leyenda manual debajo y wrapper de altura fija.

interface CategoryBarsProps {
  data: CategoryAggregation
  /** Moneda base para formatear importes en el tooltip. */
  currency?: string
  /** Texto cuando no hay datos en la ventana. */
  emptyLabel?: string
}

// Eje Y compacto en miles ("1.2k") al estilo v0: sin símbolo de moneda (va en el
// caption y el tooltip), para no recargar el eje con números largos.
function kFmt(v: number): string {
  if (Math.abs(v) >= 1000) return `${+(v / 1000).toFixed(1)}k`
  return `${v}`
}

// Forma mínima de lo que Recharts pasa al tooltip (evita `any`, regla del proyecto).
interface TooltipItem {
  dataKey: string | number
  value: number
  color: string
}

export function CategoryBars({ data, currency = 'EUR', emptyLabel }: CategoryBarsProps) {
  if (data.rows.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-card py-16 text-center text-sm text-muted-foreground">
        {emptyLabel ?? 'No hay datos en los últimos 4 años.'}
      </div>
    )
  }

  // Recharts necesita formato "ancho": una fila por categoría con una clave por año.
  const chartData = data.rows.map((row) => ({
    category: MAIN_CATEGORY_LABEL[row.category],
    ...Object.fromEntries(data.years.map((y) => [String(y), row.totals[y]])),
  }))

  // Tooltip HTML propio (estilo v0): cabecera = categoría, una línea por año con punto de color.
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
      <div className="h-80 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={chartData} margin={{ top: 8, right: 4, left: -16, bottom: 0 }} barGap={3}>
            <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
            <XAxis
              dataKey="category"
              tickLine={false}
              axisLine={false}
              tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
              interval={0}
            />
            <YAxis
              tickLine={false}
              axisLine={false}
              tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
              tickFormatter={kFmt}
            />
            <Tooltip cursor={{ fill: 'var(--muted)' }} content={<ChartTooltip />} />
            {data.years.map((year, i) => (
              <Bar
                key={year}
                dataKey={String(year)}
                name={String(year)}
                fill={`var(--chart-${i + 1})`}
                radius={[4, 4, 0, 0]}
                maxBarSize={28}
              />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Leyenda manual (estilo v0): un cuadradito de color por año. */}
      <div className="mt-4 flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
        {data.years.map((year, i) => (
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
