import { Filter, Globe, Calendar } from "lucide-react"
import { Section, SectionHeader } from "@/components/section"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { IncomeExpenseChart } from "@/components/charts/income-expense-chart"

const transactions = [
  { label: "Nómina", category: "Ingreso", amount: "+2.850 €", positive: true },
  { label: "Alquiler", category: "Vivienda", amount: "-820 €", positive: false },
  { label: "Supermercado", category: "Alimentación", amount: "-312 €", positive: false },
  { label: "Suscripciones", category: "Servicios", amount: "-46 €", positive: false },
]

const filters = [
  { icon: Calendar, label: "Mensual" },
  { icon: Globe, label: "EUR · USD · GBP" },
  { icon: Filter, label: "Por categoría" },
]

export function Expenses() {
  return (
    <Section id="gastos" className="border-b border-border">
      <SectionHeader
        eyebrow="Gastos e ingresos"
        title="Seguimiento mensual y multimoneda"
        description="Registra y filtra tus movimientos por mes, categoría y divisa. Visualiza de un vistazo cuánto entra y cuánto sale para mantener el control de tu presupuesto."
      />

      <div className="mt-10 grid gap-6 lg:grid-cols-5">
        <Card className="lg:col-span-3">
          <CardHeader>
            <CardTitle>Ingresos vs Gastos por mes</CardTitle>
            <CardDescription>Moneda base: EUR · datos ilustrativos</CardDescription>
          </CardHeader>
          <CardContent>
            <IncomeExpenseChart />
            <div className="mt-4 flex items-center gap-5 text-xs text-muted-foreground">
              <span className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-sm" style={{ backgroundColor: "var(--chart-1)" }} />
                Ingresos
              </span>
              <span className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-sm" style={{ backgroundColor: "var(--chart-2)" }} />
                Gastos
              </span>
            </div>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Movimientos recientes</CardTitle>
            <CardDescription>Junio · 2.850 € ingresos</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <div className="flex flex-wrap gap-2">
              {filters.map((f) => (
                <span
                  key={f.label}
                  className="inline-flex items-center gap-1.5 rounded-md border border-border bg-muted px-2.5 py-1 text-xs text-muted-foreground"
                >
                  <f.icon className="h-3.5 w-3.5" aria-hidden="true" />
                  {f.label}
                </span>
              ))}
            </div>
            <ul className="flex flex-col divide-y divide-border">
              {transactions.map((t) => (
                <li key={t.label} className="flex items-center justify-between py-2.5">
                  <span className="flex flex-col">
                    <span className="text-sm font-medium">{t.label}</span>
                    <span className="text-xs text-muted-foreground">{t.category}</span>
                  </span>
                  <span
                    className={`text-sm font-semibold tabular-nums ${t.positive ? "text-positive" : "text-foreground"}`}
                  >
                    {t.amount}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>
    </Section>
  )
}
