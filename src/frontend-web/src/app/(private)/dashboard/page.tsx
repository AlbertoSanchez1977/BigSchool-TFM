'use client'

import { useMemo } from 'react'
import Link from 'next/link'
import {
  BarChart, Bar,
  AreaChart, Area,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from 'recharts'
import { TrendingUp, TrendingDown, Wallet, PiggyBank, ArrowRight } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { Button }  from '@/components/ui/button'

import { useSummary }      from '@/hooks/useSummary'
import { useMonthlyChart } from '@/hooks/useMonthlyChart'
import { usePortfolios }   from '@/hooks/usePortfolios'
import { usePortfoliosSummary } from '@/hooks/usePortfoliosSummary'
import { useTransactions } from '@/hooks/useTransactions'
import {
  deriveMonthlyPoints,
  deriveCumulativeBalance,
  deriveSavingsRate,
} from '@/lib/dashboard/derive'
import {
  MAIN_CATEGORY_LABEL,
  formatAmount, formatPct, colorPnL, signPnL,
} from '@/lib/transactions/labels'
import { MAX_PAGE_SIZE } from '@/types/pagination'

const MONTH_LABELS = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic']
const MONTH_NAMES  = ['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre']

// ── Tooltip custom (mismo estilo que la landing) ──────────────────────────────

function BarTooltip({ active, payload, label, currency }: {
  active?: boolean; payload?: { dataKey: string; color: string; value: number }[]
  label?: string | number; currency: string
}) {
  if (!active || !payload?.length) return null
  const monthName = MONTH_NAMES[Number(label) - 1] ?? String(label)
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium text-popover-foreground">{monthName}</p>
      {payload.map(item => (
        <p key={item.dataKey} className="flex items-center justify-between gap-4 text-muted-foreground">
          <span className="flex items-center gap-1.5">
            <span className="h-2 w-2 rounded-full" style={{ backgroundColor: item.color }} />
            {item.dataKey === 'income' ? 'Ingresos' : 'Gastos'}
          </span>
          <span className="font-medium text-popover-foreground">{formatAmount(item.value, currency)}</span>
        </p>
      ))}
    </div>
  )
}

function AreaTooltip({ active, payload, label, currency }: {
  active?: boolean; payload?: { value: number }[]
  label?: string | number; currency: string
}) {
  if (!active || !payload?.length) return null
  const monthName = MONTH_NAMES[Number(label) - 1] ?? String(label)
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium text-popover-foreground">{monthName}</p>
      <p className="flex items-center justify-between gap-4 text-muted-foreground">
        <span>Balance acumulado</span>
        <span className="font-medium text-popover-foreground">{formatAmount(payload[0].value, currency)}</span>
      </p>
    </div>
  )
}

// ── Subcomponentes de sección ─────────────────────────────────────────────────

function SectionTitle({ children }: { children: React.ReactNode }) {
  return <h2 className="mb-4 text-base font-semibold">{children}</h2>
}

// ── Página ────────────────────────────────────────────────────────────────────

export default function DashboardPage() {
  const today = new Date()
  const currentYear  = today.getFullYear()
  const currentMonth = today.getMonth() + 1   // 1-indexed; corta los arrays al mes en curso

  // Rango del mes en curso para el summary (primer día → hoy)
  const from = `${currentYear}-${String(currentMonth).padStart(2, '0')}-01`
  const to   = today.toISOString().split('T')[0]

  const { data: summary,       isLoading: summaryLoading    } = useSummary(from, to)
  const { data: rawPoints,     isLoading: chartLoading      } = useMonthlyChart(currentYear)
  // El mini-resumen quiere todas las carteras del usuario; pageSize alto en vez de
  // paginar aquí (es un widget de dashboard, no un listado — la Pagination real vive en /investments).
  const { data: portfoliosPage, isLoading: portfoliosLoading } = usePortfolios({ page: 1, pageSize: MAX_PAGE_SIZE })
  const portfolios = portfoliosPage?.items
  // Agregado real del backend (GET /portfolios/summary) — sustituye la suma en cliente
  // que antes hacía derivePortfolioTotals sobre la lista de carteras.
  const { data: invSummary, isLoading: invSummaryLoading } = usePortfoliosSummary()
  const { data: recentTxns,    isLoading: txnsLoading       } = useTransactions({ page: 1, pageSize: 5 })

  // Solo mostramos hasta el mes actual — meses futuros ni barras ni línea plana
  const monthlyPoints    = useMemo(
    () => deriveMonthlyPoints(rawPoints ?? []).slice(0, currentMonth),
    [rawPoints, currentMonth]
  )
  const cumulativePoints = useMemo(
    () => deriveCumulativeBalance(rawPoints ?? []).slice(0, currentMonth),
    [rawPoints, currentMonth]
  )
  const savingsRate      = useMemo(
    () => summary ? deriveSavingsRate(summary.totalIncome, summary.totalExpense) : null,
    [summary]
  )

  const cur         = summary?.baseCurrency ?? 'EUR'
  const portfolioCur = invSummary?.baseCurrency ?? cur

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
              valueColor="text-positive"
            />
            <KpiCard
              icon={<TrendingDown className="size-4" />}
              label="Gastos (mes)"
              value={formatAmount(summary?.totalExpense ?? 0, cur)}
              valueColor="text-negative"
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
              value={savingsRate !== null ? formatPct(savingsRate) : '—'}
              valueColor={colorPnL(savingsRate ?? 0)}
            />
          </>
        )}
      </section>

      {/* ── 2. Gráficas Ingresos/Gastos + Balance acumulado ───────────────────── */}
      <div className="mt-8 grid grid-cols-1 gap-6 lg:grid-cols-2">

        {/* Barras mensuales — mismos colores que IncomeExpenseChart de la landing */}
        <div className="rounded-lg border border-border bg-card p-4">
          <SectionTitle>Ingresos vs Gastos — {currentYear}</SectionTitle>
          {chartLoading ? (
            <Skeleton className="h-52 w-full rounded" />
          ) : (
            <>
              <div className="h-56 w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={monthlyPoints} margin={{ top: 8, right: 4, left: -16, bottom: 0 }} barGap={4}>
                    <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
                    <XAxis
                      dataKey="month"
                      tickFormatter={m => MONTH_LABELS[Number(m) - 1]}
                      tickLine={false}
                      axisLine={false}
                      tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
                    />
                    <YAxis
                      tickFormatter={v => `${Number(v) / 1000}k`}
                      tickLine={false}
                      axisLine={false}
                      tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                    />
                    <Tooltip cursor={{ fill: 'var(--muted)' }} content={<BarTooltip currency={cur} />} />
                    <Bar dataKey="income"  fill="var(--chart-1)" radius={[4,4,0,0]} maxBarSize={22} />
                    <Bar dataKey="expense" fill="var(--chart-2)" radius={[4,4,0,0]} maxBarSize={22} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
              <div className="mt-3 flex items-center gap-5 text-xs text-muted-foreground">
                <span className="flex items-center gap-1.5">
                  <span className="h-2.5 w-2.5 rounded-sm" style={{ backgroundColor: 'var(--chart-1)' }} />
                  Ingresos
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="h-2.5 w-2.5 rounded-sm" style={{ backgroundColor: 'var(--chart-2)' }} />
                  Gastos
                </span>
              </div>
            </>
          )}
        </div>

        {/* Area chart balance acumulado — mismo estilo que PortfolioChart de la landing */}
        <div className="rounded-lg border border-border bg-card p-4">
          <SectionTitle>Balance acumulado — {currentYear}</SectionTitle>
          {chartLoading ? (
            <Skeleton className="h-52 w-full rounded" />
          ) : (
            <div className="h-56 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={cumulativePoints} margin={{ top: 8, right: 4, left: -16, bottom: 0 }}>
                  <defs>
                    <linearGradient id="dashboardBalanceFill" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%"   stopColor="var(--chart-5)" stopOpacity={0.25} />
                      <stop offset="100%" stopColor="var(--chart-5)" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
                  <XAxis
                    dataKey="month"
                    tickFormatter={m => MONTH_LABELS[Number(m) - 1]}
                    tickLine={false}
                    axisLine={false}
                    tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
                  />
                  <YAxis
                    tickFormatter={v => `${Number(v) / 1000}k`}
                    tickLine={false}
                    axisLine={false}
                    tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                    domain={['dataMin - 200', 'dataMax + 200']}
                  />
                  <Tooltip cursor={{ stroke: 'var(--border)' }} content={<AreaTooltip currency={cur} />} />
                  <Area
                    type="monotone"
                    dataKey="balance"
                    stroke="var(--chart-5)"
                    strokeWidth={2.5}
                    fill="url(#dashboardBalanceFill)"
                    dot={false}
                    activeDot={{ r: 4, fill: 'var(--chart-5)' }}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
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

        {portfoliosLoading || invSummaryLoading ? (
          <Skeleton className="h-24 rounded-lg" />
        ) : !portfolios || portfolios.length === 0 ? (
        <div
          className="rounded-lg border border-border bg-card py-12 text-center"
          data-testid="empty-state"
        >
          <TrendingUp className="mx-auto mb-3 h-8 w-8 text-muted-foreground/40" />
          <p className="text-sm text-muted-foreground">No tienes carteras aún.</p>
          <Button variant="outline" size="sm" className="mt-4">
            <Link href="/investments">Crea mi primera cartera</Link>
          </Button>
        </div>
        ) : (
          <div className="rounded-lg border border-border bg-card p-4">
            {/* Agregado total */}
            <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
              <div>
                <p className="text-xs text-muted-foreground">Valor de mercado</p>
                <p className="text-sm font-semibold tabular-nums">
                  {formatAmount(invSummary?.marketValue ?? 0, portfolioCur)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Coste base</p>
                <p className="text-sm font-semibold tabular-nums">
                  {formatAmount(invSummary?.costBasis ?? 0, portfolioCur)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">PnL total</p>
                <p className={`text-sm font-semibold tabular-nums ${colorPnL(invSummary?.totalPnL ?? 0)}`}>
                  {signPnL(invSummary?.totalPnL ?? 0)}{formatAmount(invSummary?.totalPnL ?? 0, portfolioCur)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Rentabilidad</p>
                <p className={`text-sm font-semibold tabular-nums ${colorPnL(invSummary?.returnPct ?? 0)}`}>
                  {signPnL(invSummary?.returnPct ?? 0)}{formatPct(invSummary?.returnPct ?? 0, 2)}
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
          <div className="rounded-lg border border-border bg-card py-12 text-center">
            <TrendingDown className="mx-auto mb-3 h-8 w-8 text-muted-foreground/40" />
            <p className="text-sm text-muted-foreground">No hay transacciones registradas.</p>
            <Button variant="outline" size="sm" className="mt-4">
              <Link href="/expenses">Nueva transacción</Link>
            </Button>
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
                <p className={`ml-4 shrink-0 tabular-nums font-semibold ${tx.type === 'Income' ? 'text-positive' : 'text-negative'}`}>
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
