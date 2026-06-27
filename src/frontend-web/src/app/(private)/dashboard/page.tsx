'use client'

import { useMemo } from 'react'
import Link from 'next/link'
import {
  BarChart, Bar,
  LineChart, Line,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from 'recharts'
import { TrendingUp, TrendingDown, Wallet, PiggyBank, BarChart2, ArrowRight } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { useSummary }      from '@/hooks/useSummary'
import { useMonthlyChart } from '@/hooks/useMonthlyChart'
import { usePortfolios }   from '@/hooks/usePortfolios'
import { useTransactions } from '@/hooks/useTransactions'
import {
  deriveMonthlyPoints,
  deriveCumulativeBalance,
  deriveSavingsRate,
  derivePortfolioTotals,
} from '@/lib/dashboard/derive'
import { MAIN_CATEGORY_LABEL } from '@/lib/transactions/labels'

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatAmount(amount: number, currency: string): string {
  if (!currency) return amount.toFixed(2)
  try {
    return new Intl.NumberFormat('es-ES', {
      style: 'currency', currency, minimumFractionDigits: 2,
    }).format(amount)
  } catch {
    return `${currency} ${amount.toFixed(2)}`
  }
}

function colorPnL(value: number) {
  if (value > 0) return 'text-[hsl(var(--positive))]'
  if (value < 0) return 'text-[hsl(var(--negative))]'
  return ''
}

function signPnL(value: number) {
  return value > 0 ? '+' : ''
}

const MONTH_LABELS = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic']

// ── Subcomponentes de sección ─────────────────────────────────────────────────

function SectionTitle({ children }: { children: React.ReactNode }) {
  return <h2 className="mb-4 text-base font-semibold">{children}</h2>
}

// ── Página ────────────────────────────────────────────────────────────────────

export default function DashboardPage() {
  const today = new Date()
  const currentYear = today.getFullYear()

  // Rango del mes en curso para el summary (primer día → hoy)
  const from = `${currentYear}-${String(today.getMonth() + 1).padStart(2, '0')}-01`
  const to   = today.toISOString().split('T')[0]

  const { data: summary,    isLoading: summaryLoading    } = useSummary(from, to)
  const { data: rawPoints,  isLoading: chartLoading      } = useMonthlyChart(currentYear)
  const { data: portfolios, isLoading: portfoliosLoading } = usePortfolios()
  const { data: recentTxns, isLoading: txnsLoading       } = useTransactions({ page: 1, pageSize: 5 })

  const monthlyPoints    = useMemo(() => deriveMonthlyPoints(rawPoints ?? []),    [rawPoints])
  const cumulativePoints = useMemo(() => deriveCumulativeBalance(rawPoints ?? []), [rawPoints])
  const portfolioTotals  = useMemo(() => derivePortfolioTotals(portfolios ?? []), [portfolios])
  const savingsRate      = useMemo(
    () => summary ? deriveSavingsRate(summary.totalIncome, summary.totalExpense) : null,
    [summary]
  )

  const cur         = summary?.baseCurrency ?? 'EUR'
  const portfolioCur = portfolios?.[0]?.realizedPnLCurrency ?? cur

  // ── Formateo para tooltip de Recharts (ValueType = number | string | Array<...>) ─
  const fmtTooltip = (v: unknown) => formatAmount(Number(v ?? 0), cur)
  const fmtLabel   = (m: unknown) => MONTH_LABELS[Number(m) - 1] ?? ''

  return (
    <div className="mx-auto max-w-6xl px-5 py-10 md:px-8">
      <h1 className="font-heading text-2xl font-semibold">Dashboard</h1>

      {/* ── 1. KPI cards ──────────────────────────────────────────────────────── */}
      <section className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        {summaryLoading ? (
          Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20 rounded-lg" />
          ))
        ) : (
          <>
            <KpiCard
              icon={<TrendingUp className="size-4" />}
              label="Ingresos (mes)"
              value={formatAmount(summary?.totalIncome ?? 0, cur)}
              valueColor="text-[hsl(var(--positive))]"
            />
            <KpiCard
              icon={<TrendingDown className="size-4" />}
              label="Gastos (mes)"
              value={formatAmount(summary?.totalExpense ?? 0, cur)}
              valueColor="text-[hsl(var(--negative))]"
            />
            <KpiCard
              icon={<Wallet className="size-4" />}
              label="Balance (mes)"
              value={formatAmount(summary?.balance ?? 0, cur)}
              valueColor={colorPnL(summary?.balance ?? 0)}
            />
            <KpiCard
              icon={<PiggyBank className="size-4" />}
              label="Tasa de ahorro"
              value={savingsRate !== null ? `${savingsRate.toFixed(1)} %` : '—'}
              valueColor={colorPnL(savingsRate ?? 0)}
            />
          </>
        )}
      </section>

      {/* ── 2. Gráficas Ingresos/Gastos + Balance acumulado ───────────────────── */}
      <div className="mt-8 grid grid-cols-1 gap-6 lg:grid-cols-2">

        {/* Barras mensuales */}
        <div className="rounded-lg border border-border bg-card p-4">
          <SectionTitle>Ingresos vs Gastos — {currentYear}</SectionTitle>
          {chartLoading ? (
            <Skeleton className="h-52 w-full rounded" />
          ) : (
            <ResponsiveContainer width="100%" height={210}>
              <BarChart data={monthlyPoints} barSize={7} barCategoryGap="30%">
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="hsl(var(--border))" />
                <XAxis
                  dataKey="month"
                  tickFormatter={m => MONTH_LABELS[Number(m) - 1]}
                  tick={{ fontSize: 10 }}
                  tickLine={false}
                  axisLine={false}
                />
                <YAxis
                  tickFormatter={v => `${(Number(v) / 1000).toFixed(0)}k`}
                  tick={{ fontSize: 10 }}
                  tickLine={false}
                  axisLine={false}
                  width={32}
                />
                <Tooltip formatter={fmtTooltip} labelFormatter={fmtLabel} />
                <Bar dataKey="income"  name="Ingresos" fill="hsl(var(--positive))" radius={[3,3,0,0]} />
                <Bar dataKey="expense" name="Gastos"   fill="hsl(var(--negative))" radius={[3,3,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>

        {/* Línea balance acumulado */}
        <div className="rounded-lg border border-border bg-card p-4">
          <SectionTitle>Balance acumulado — {currentYear}</SectionTitle>
          {chartLoading ? (
            <Skeleton className="h-52 w-full rounded" />
          ) : (
            <ResponsiveContainer width="100%" height={210}>
              <LineChart data={cumulativePoints}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="hsl(var(--border))" />
                <XAxis
                  dataKey="month"
                  tickFormatter={m => MONTH_LABELS[Number(m) - 1]}
                  tick={{ fontSize: 10 }}
                  tickLine={false}
                  axisLine={false}
                />
                <YAxis
                  tickFormatter={v => `${(Number(v) / 1000).toFixed(0)}k`}
                  tick={{ fontSize: 10 }}
                  tickLine={false}
                  axisLine={false}
                  width={32}
                />
                <Tooltip formatter={fmtTooltip} labelFormatter={fmtLabel} />
                <Line
                  type="monotone"
                  dataKey="balance"
                  name="Balance acumulado"
                  stroke="hsl(var(--chart-line))"
                  strokeWidth={2}
                  dot={false}
                  activeDot={{ r: 4 }}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {/* ── 3. Mini-resumen de inversiones ────────────────────────────────────── */}
      <section className="mt-8">
        <div className="mb-4 flex items-center justify-between">
          <SectionTitle>Inversiones</SectionTitle>
          <Link
            href="/investments"
            className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            Ver carteras <ArrowRight className="size-3" />
          </Link>
        </div>

        {portfoliosLoading ? (
          <Skeleton className="h-24 rounded-lg" />
        ) : !portfolios || portfolios.length === 0 ? (
          <div className="rounded-lg border border-border bg-card p-6 text-center text-sm text-muted-foreground">
            No hay carteras. <Link href="/investments" className="underline">Crear una</Link>
          </div>
        ) : (
          <div className="rounded-lg border border-border bg-card p-4">
            {/* Agregado total */}
            <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
              <div>
                <p className="text-xs text-muted-foreground">Valor de mercado</p>
                <p className="text-sm font-semibold tabular-nums">
                  {formatAmount(portfolioTotals.marketValue, portfolioCur)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Coste base</p>
                <p className="text-sm font-semibold tabular-nums">
                  {formatAmount(portfolioTotals.costBasis, portfolioCur)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">PnL total</p>
                <p className={`text-sm font-semibold tabular-nums ${colorPnL(portfolioTotals.totalPnL)}`}>
                  {signPnL(portfolioTotals.totalPnL)}{formatAmount(portfolioTotals.totalPnL, portfolioCur)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Rentabilidad</p>
                <p className={`text-sm font-semibold tabular-nums ${colorPnL(portfolioTotals.returnPct ?? 0)}`}>
                  {portfolioTotals.returnPct !== null
                    ? `${signPnL(portfolioTotals.returnPct)}${portfolioTotals.returnPct.toFixed(2)} %`
                    : '—'}
                </p>
              </div>
            </div>

            {/* Lista de carteras */}
            <div className="divide-y divide-border">
              {portfolios.map(p => (
                <Link key={p.idPortfolio} href={`/investments/${p.idPortfolio}`}
                  className="flex items-center justify-between py-2 text-sm hover:text-primary"
                >
                  <span className="font-medium">{p.name}</span>
                  <span className={`tabular-nums ${colorPnL(p.unrealizedPnL)}`}>
                    {signPnL(p.unrealizedPnL)}{formatAmount(p.unrealizedPnL, p.realizedPnLCurrency)}
                  </span>
                </Link>
              ))}
            </div>

            {/* TODO (deuda técnica backend): top holdings entre carteras requiere un endpoint
                GET /portfolios/top-holdings o bien extender GET /portfolios con el holding
                de mayor marketValue de cada cartera. Contrato esperado:
                [{ idPortfolio, idCompany, ticker, openShares, marketValue, unrealizedPnLPct }] */}
          </div>
        )}
      </section>

      {/* ── 4. Últimas 5 transacciones ────────────────────────────────────────── */}
      <section className="mt-8 pb-10">
        <div className="mb-4 flex items-center justify-between">
          <SectionTitle>Últimas transacciones</SectionTitle>
          <Link
            href="/expenses"
            className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            Ver todas <ArrowRight className="size-3" />
          </Link>
        </div>

        {txnsLoading ? (
          <div className="flex flex-col gap-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-12 rounded-lg" />
            ))}
          </div>
        ) : !recentTxns?.items.length ? (
          <div className="rounded-lg border border-border bg-card p-6 text-center text-sm text-muted-foreground">
            No hay transacciones. <Link href="/expenses" className="underline">Registrar una</Link>
          </div>
        ) : (
          <div className="rounded-lg border border-border bg-card divide-y divide-border">
            {recentTxns.items.map(tx => (
              <div key={tx.idTransaction} className="flex items-center justify-between px-4 py-3 text-sm">
                <div className="min-w-0">
                  <p className="truncate font-medium">
                    {tx.description ?? MAIN_CATEGORY_LABEL[tx.idMainCategory] ?? tx.idMainCategory}
                  </p>
                  <p className="text-xs text-muted-foreground">{tx.transactionDate}</p>
                </div>
                <p className={`ml-4 shrink-0 tabular-nums font-semibold ${tx.type === 'Income' ? 'text-[hsl(var(--positive))]' : 'text-[hsl(var(--negative))]'}`}>
                  {tx.type === 'Income' ? '+' : '−'}{formatAmount(tx.originalAmount, tx.originalCurrency)}
                </p>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}

// ── KpiCard ───────────────────────────────────────────────────────────────────

function KpiCard({
  icon, label, value, valueColor = '',
}: {
  icon: React.ReactNode
  label: string
  value: string
  valueColor?: string
}) {
  return (
    <div className="rounded-lg border border-border bg-card p-4">
      <div className="mb-1 flex items-center gap-1.5 text-xs text-muted-foreground">
        {icon}
        {label}
      </div>
      <p className={`text-base font-semibold tabular-nums ${valueColor}`}>{value}</p>
    </div>
  )
}
