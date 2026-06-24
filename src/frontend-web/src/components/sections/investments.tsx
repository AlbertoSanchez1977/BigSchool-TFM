import { TrendingUp, Wallet, Percent } from "lucide-react"
import { Section, SectionHeader } from "@/components/section"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { PortfolioChart } from "@/components/charts/portfolio-chart"

const kpis = [
  { icon: Wallet, label: "Valor de mercado", value: "6.500 €", hint: "3 posiciones", tone: "neutral" },
  { icon: TrendingUp, label: "PnL total", value: "+540 €", hint: "Realizado + no realizado", tone: "positive" },
  { icon: Percent, label: "Retorno", value: "+9,1 %", hint: "Desde el inicio", tone: "positive" },
]

const holdings = [
  { ticker: "AAPL", name: "Apple Inc.", market: "2.640 €", pnl: "+312 €", positive: true },
  { ticker: "MSFT", name: "Microsoft Corp.", market: "2.310 €", pnl: "+198 €", positive: true },
  { ticker: "SAN", name: "Banco Santander", market: "1.550 €", pnl: "+30 €", positive: true },
]

export function Investments() {
  return (
    <Section id="inversiones" className="border-b border-border bg-muted/30">
      <SectionHeader
        eyebrow="Inversiones"
        title="Carteras, ventas FIFO y rentabilidad multimoneda"
        description="Gestiona tus posiciones con cálculo de ventas por método FIFO y consulta tu rentabilidad realizada y no realizada en cualquier divisa, consolidada en tu moneda base."
      />

      <div className="mt-10 grid gap-4 sm:grid-cols-3">
        {kpis.map((kpi) => (
          <Card key={kpi.label} size="sm">
            <CardContent className="flex items-center gap-3">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
                <kpi.icon className="h-5 w-5" aria-hidden="true" />
              </span>
              <div>
                <p className="text-xs text-muted-foreground">{kpi.label}</p>
                <p
                  className={`font-heading text-xl font-semibold tabular-nums ${
                    kpi.tone === "positive" ? "text-positive" : "text-foreground"
                  }`}
                >
                  {kpi.value}
                </p>
                <p className="text-[11px] text-muted-foreground">{kpi.hint}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-5">
        <Card className="lg:col-span-3">
          <CardHeader>
            <CardTitle>Valor de cartera</CardTitle>
            <CardDescription>Evolución mensual · EUR</CardDescription>
          </CardHeader>
          <CardContent>
            <PortfolioChart />
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Posiciones</CardTitle>
            <CardDescription>Rentabilidad no realizada</CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col divide-y divide-border">
              {holdings.map((h) => (
                <li key={h.ticker} className="flex items-center justify-between py-3">
                  <span className="flex flex-col">
                    <span className="text-sm font-semibold">{h.ticker}</span>
                    <span className="text-xs text-muted-foreground">{h.name}</span>
                  </span>
                  <span className="flex flex-col items-end">
                    <span className="text-sm font-medium tabular-nums">{h.market}</span>
                    <span
                      className={`text-xs font-medium tabular-nums ${h.positive ? "text-positive" : "text-negative"}`}
                    >
                      {h.pnl}
                    </span>
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
