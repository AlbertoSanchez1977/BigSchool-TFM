'use client'

import { useState } from 'react'
import {
  LineChart, Line, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import { Skeleton } from '@/components/ui/skeleton'
import { useValuationSeries } from '@/hooks/useValuations'
import { formatAmount, formatDate, colorPnL, signPnL, formatPct } from '@/lib/transactions/labels'
import { VALUATION_PERIOD_LABEL } from '@/lib/investments/labels'
import { VALUATION_PERIODS } from '@/types/enums'
import type { ValuationPeriod } from '@/types/enums'

function SeriesTooltip({ active, payload, label, currency }: {
  active?: boolean; payload?: { value: number }[]; label?: string; currency: string
}) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium text-popover-foreground">{label ? formatDate(label) : ''}</p>
      <p className="flex items-center justify-between gap-4 text-muted-foreground">
        <span>Precio</span>
        <span className="font-medium text-popover-foreground">{formatAmount(payload[0].value, currency)}</span>
      </p>
    </div>
  )
}

// Serie de cotización + summary (min/max/último/variación %) de una empresa, con selector
// de periodo. Sin acciones de ver/borrar valoración individual — el backend no las expone
// (deuda documentada, spec 011 §6); el alta se hace desde CreateValuationModal en la página.
export function ValuationSeriesChart({ companyId }: { companyId: number }) {
  const [period, setPeriod] = useState<ValuationPeriod>('OneYear')
  const { data, isLoading, isError } = useValuationSeries(companyId, period)

  return (
    <div className="rounded-lg border border-border bg-card p-4">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-base font-semibold">Serie de cotización</h2>
        <div
          className="inline-flex rounded-lg border border-border p-0.5"
          role="tablist"
          aria-label="Periodo de la serie"
        >
          {VALUATION_PERIODS.map((p) => (
            <button
              key={p}
              type="button"
              role="tab"
              aria-selected={period === p}
              onClick={() => setPeriod(p)}
              data-testid={`period-${p}`}
              className={`rounded-md px-2.5 py-1 text-xs font-medium transition-colors ${
                period === p
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              {VALUATION_PERIOD_LABEL[p]}
            </button>
          ))}
        </div>
      </div>

      {isLoading && <Skeleton className="h-52 w-full rounded" data-testid="series-loading" />}

      {isError && (
        <div className="py-10 text-center text-sm text-destructive" data-testid="series-error">
          Error al cargar la serie de cotización.
        </div>
      )}

      {!isLoading && !isError && data && data.points.length === 0 && (
        <div className="py-10 text-center text-sm text-muted-foreground" data-testid="series-empty">
          Esta empresa aún no tiene valoraciones.
        </div>
      )}

      {!isLoading && !isError && data && data.points.length > 0 && (
        <>
          <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div>
              <p className="text-xs text-muted-foreground">Mínimo</p>
              <p className="text-sm font-medium tabular-nums">{formatAmount(data.summary.min, data.currency)}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Máximo</p>
              <p className="text-sm font-medium tabular-nums">{formatAmount(data.summary.max, data.currency)}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Último</p>
              <p className="text-sm font-semibold tabular-nums">{formatAmount(data.summary.last, data.currency)}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Variación</p>
              <p className={`text-sm font-semibold tabular-nums ${colorPnL(data.summary.changePct)}`}>
                {signPnL(data.summary.changePct)}{formatPct(data.summary.changePct, 2)}
              </p>
            </div>
          </div>

          <div className="h-56 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={data.points} margin={{ top: 8, right: 4, left: -16, bottom: 0 }}>
                <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
                <XAxis
                  dataKey="date"
                  tickFormatter={(d: string) => formatDate(d)}
                  tickLine={false}
                  axisLine={false}
                  tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                  minTickGap={24}
                />
                <YAxis
                  tickLine={false}
                  axisLine={false}
                  tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                  domain={['dataMin - 1', 'dataMax + 1']}
                />
                <Tooltip cursor={{ stroke: 'var(--border)' }} content={<SeriesTooltip currency={data.currency} />} />
                <Line
                  type="monotone"
                  dataKey="price"
                  stroke="var(--chart-5)"
                  strokeWidth={2.5}
                  dot={false}
                  activeDot={{ r: 4, fill: 'var(--chart-5)' }}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </>
      )}
    </div>
  )
}
