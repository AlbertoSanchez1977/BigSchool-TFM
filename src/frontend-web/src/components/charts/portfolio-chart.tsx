"use client"

import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts"

// Datos FAKE: evolución del valor de cartera (EUR).
const data = [
  { mes: "Ene", valor: 5720 },
  { mes: "Feb", valor: 5810 },
  { mes: "Mar", valor: 5680 },
  { mes: "Abr", valor: 6010 },
  { mes: "May", valor: 6240 },
  { mes: "Jun", valor: 6180 },
  { mes: "Jul", valor: 6420 },
  { mes: "Ago", valor: 6500 },
]

const eur = (v: number) => `${v.toLocaleString("es-ES")} €`

function ChartTooltip({ active, payload, label }: any) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium text-popover-foreground">{label}</p>
      <p className="flex items-center justify-between gap-4 text-muted-foreground">
        <span>Valor de cartera</span>
        <span className="font-medium text-popover-foreground">{eur(payload[0].value)}</span>
      </p>
    </div>
  )
}

export function PortfolioChart() {
  return (
    <div className="h-56 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 8, right: 4, left: -16, bottom: 0 }}>
          <defs>
            <linearGradient id="portfolioFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="var(--chart-5)" stopOpacity={0.25} />
              <stop offset="100%" stopColor="var(--chart-5)" stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
          <XAxis
            dataKey="mes"
            tickLine={false}
            axisLine={false}
            tick={{ fill: "var(--muted-foreground)", fontSize: 12 }}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
            tickFormatter={(v) => `${v / 1000}k`}
            domain={["dataMin - 200", "dataMax + 200"]}
          />
          <Tooltip cursor={{ stroke: "var(--border)" }} content={<ChartTooltip />} />
          <Area
            type="monotone"
            dataKey="valor"
            stroke="var(--chart-5)"
            strokeWidth={2.5}
            fill="url(#portfolioFill)"
            dot={false}
            activeDot={{ r: 4, fill: "var(--chart-5)" }}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  )
}
