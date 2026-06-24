"use client"

import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts"

// Datos FAKE: Ingresos vs Gastos (EUR) por mes.
// Cada serie usa un color de la paleta categórica "por año".
const data = [
  { mes: "Ene", ingresos: 2850, gastos: 1980 },
  { mes: "Feb", ingresos: 2850, gastos: 2140 },
  { mes: "Mar", ingresos: 3120, gastos: 1860 },
  { mes: "Abr", ingresos: 2850, gastos: 2310 },
  { mes: "May", ingresos: 2850, gastos: 2020 },
  { mes: "Jun", ingresos: 3050, gastos: 2180 },
]

const eur = (v: number) => `${v.toLocaleString("es-ES")} €`

function ChartTooltip({ active, payload, label }: any) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium text-popover-foreground">{label}</p>
      {payload.map((item: any) => (
        <p key={item.dataKey} className="flex items-center justify-between gap-4 text-muted-foreground">
          <span className="flex items-center gap-1.5">
            <span className="h-2 w-2 rounded-full" style={{ backgroundColor: item.color }} />
            {item.dataKey === "ingresos" ? "Ingresos" : "Gastos"}
          </span>
          <span className="font-medium text-popover-foreground">{eur(item.value)}</span>
        </p>
      ))}
    </div>
  )
}

export function IncomeExpenseChart() {
  return (
    <div className="h-56 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} margin={{ top: 8, right: 4, left: -16, bottom: 0 }} barGap={4}>
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
          />
          <Tooltip cursor={{ fill: "var(--muted)" }} content={<ChartTooltip />} />
          <Bar dataKey="ingresos" fill="var(--chart-1)" radius={[4, 4, 0, 0]} maxBarSize={22} />
          <Bar dataKey="gastos" fill="var(--chart-2)" radius={[4, 4, 0, 0]} maxBarSize={22} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
