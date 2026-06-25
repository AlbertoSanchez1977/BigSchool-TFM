"use client"

import Link from 'next/link'
import { ArrowUpRight, Sparkles, TrendingUp, Wallet } from "lucide-react"
import { Button } from "@/components/ui/button"
import { PortfolioChart } from "@/components/charts/portfolio-chart"
import { useAuth } from '@/hooks/useAuth'

export function Hero() {
  const { user, isLoading } = useAuth()

  return (
    <section className="relative w-full overflow-hidden border-b border-border">
      <div className="mx-auto grid w-full max-w-6xl items-center gap-12 px-5 py-16 md:grid-cols-2 md:px-8 md:py-24">
        <div className="flex flex-col items-start gap-6">
          <span className="inline-flex items-center gap-2 rounded-full border border-border bg-muted px-3 py-1 text-xs font-medium text-muted-foreground">
            <Sparkles className="h-3.5 w-3.5 text-primary" aria-hidden="true" />
            Finanzas e inversión con asistente de IA
          </span>
          <h1 className="text-balance font-heading text-4xl font-semibold leading-tight tracking-tight md:text-5xl">
            Controla tus gastos e inversiones con la ayuda de la inteligencia artificial
          </h1>
          <p className="max-w-md text-pretty text-lg leading-relaxed text-muted-foreground">
            BigSchool reúne tus finanzas personales y tus carteras de value investing en un solo lugar: seguimiento
            multimoneda, rentabilidad realizada y no realizada, y un asistente que te ayuda a decidir.
          </p>
           { !isLoading && !user && (
          <div className="flex flex-col gap-3 sm:flex-row">
            <Button size="lg" className="gap-1.5" render={<Link href="/register" />} nativeButton={false}>
              Registrarse
              <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
            </Button>
            <Button size="lg" variant="outline" render={<Link href="/login" />} nativeButton={false}>
              Iniciar sesión
            </Button>
          </div>
          )}
          <div className="flex items-center gap-6 pt-2 text-sm text-muted-foreground">
            <span className="flex items-center gap-2">
              <Wallet className="h-4 w-4 text-primary" aria-hidden="true" />
              Multimoneda
            </span>
            <span className="flex items-center gap-2">
              <TrendingUp className="h-4 w-4 text-primary" aria-hidden="true" />
              Ventas FIFO
            </span>
          </div>
        </div>

        <div className="relative">
          <div className="rounded-2xl border border-border bg-card p-5 shadow-sm ring-1 ring-foreground/5">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground">Valor de cartera</p>
                <p className="font-heading text-2xl font-semibold tabular-nums">6.500 €</p>
              </div>
              <span className="inline-flex items-center gap-1 rounded-full bg-positive/10 px-2.5 py-1 text-xs font-medium text-positive">
                <TrendingUp className="h-3.5 w-3.5" aria-hidden="true" />
                +9,1 %
              </span>
            </div>
            <div className="mt-4">
              <PortfolioChart />
            </div>
            <div className="mt-4 grid grid-cols-3 gap-3 border-t border-border pt-4">
              <div>
                <p className="text-[11px] text-muted-foreground">Nómina</p>
                <p className="text-sm font-semibold tabular-nums">2.850 €</p>
              </div>
              <div>
                <p className="text-[11px] text-muted-foreground">PnL total</p>
                <p className="text-sm font-semibold tabular-nums text-positive">+540 €</p>
              </div>
              <div>
                <p className="text-[11px] text-muted-foreground">Retorno</p>
                <p className="text-sm font-semibold tabular-nums text-positive">+9,1 %</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
