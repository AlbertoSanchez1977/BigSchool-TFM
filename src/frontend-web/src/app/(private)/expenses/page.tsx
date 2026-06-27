'use client'

import { useMemo, useState } from 'react'
import { ChevronLeft, ChevronRight, Plus, StickyNote, TrendingDown, TrendingUp, Wallet } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { TransactionSheet } from '@/components/transactions/transaction-sheet'
import { CategoryBars } from '@/components/charts/category-bars'
import { useTransactions } from '@/hooks/useTransactions'
import { useSummary } from '@/hooks/useSummary'
import { useCategories } from '@/hooks/useCategories'
import { useCategoryChart } from '@/hooks/useCategoryChart'
import {
  TRANSACTION_TYPE_LABEL, MAIN_CATEGORY_LABEL, formatAmount, formatDate,
} from '@/lib/transactions/labels'
import type { Transaction } from '@/types/transactions'
import type { TransactionType } from '@/types/enums'

// ── Helpers de fecha ──────────────────────────────────────────────────────────

function monthRange(year: number, month: number): { from: string; to: string } {
  const from    = `${year}-${String(month).padStart(2, '0')}-01`
  const lastDay = new Date(year, month, 0).getDate()
  const to      = `${year}-${String(month).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`
  return { from, to }
}

const MONTH_NAMES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
]

// ── Componentes internos ──────────────────────────────────────────────────────

function SummaryCard({
  label, amount, currency, icon, colorClass,
}: {
  label: string
  amount: number
  currency: string
  icon: React.ReactNode
  colorClass: string
}) {
  return (
    <div className="rounded-lg border border-border bg-card p-4">
      <div className="mb-1 flex items-center gap-2 text-sm text-muted-foreground">
        {icon}
        {label}
      </div>
      <p className={`text-xl font-semibold ${colorClass}`}>
        {formatAmount(amount, currency)}
      </p>
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function ExpensesPage() {
  const now   = new Date()
  const [year,  setYear]  = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const [page,  setPage]  = useState(1)
  const pageSize = 20

  // Estado del Sheet: undefined = modo crear; Transaction = modo editar
  const [editingTx, setEditingTx] = useState<Transaction | undefined>(undefined)
  const [sheetOpen, setSheetOpen] = useState(false)

  // Pestañas: listado (por defecto) vs gráficas. El tipo de la gráfica (gasto/ingreso).
  const [tab, setTab] = useState<'list' | 'charts'>('list')
  const [chartType, setChartType] = useState<TransactionType>('Expense')

  const { from, to } = monthRange(year, month)

  const { data, isLoading, isError } = useTransactions({ from, to, page, pageSize })
  const summaryQ = useSummary(from, to)

  // Datos de la gráfica (últimos 4 años). Diferido hasta abrir la pestaña (enabled).
  // El hook es la costura que oculta si la agregación es cliente (hoy) o backend (futuro).
  const chart = useCategoryChart(chartType, now.getFullYear(), { enabled: tab === 'charts' })

  // El listado trae idSubCategory como número; resolvemos su nombre con /categories.
  // Map<idSubCategory, name> construido una vez (useMemo) a partir de todas las categorías.
  const { data: categories } = useCategories()
  const subCatNameById = useMemo(() => {
    const m = new Map<number, string>()
    for (const c of categories ?? []) {
      for (const s of c.subCategories) m.set(s.idSubCategory, s.name)
    }
    return m
  }, [categories])

  const subCatName = (id: number | null) => (id != null ? subCatNameById.get(id) ?? null : null)

  function prevMonth() {
    setPage(1)
    if (month === 1) { setMonth(12); setYear((y) => y - 1) }
    else setMonth((m) => m - 1)
  }

  function nextMonth() {
    setPage(1)
    if (month === 12) { setMonth(1); setYear((y) => y + 1) }
    else setMonth((m) => m + 1)
  }

  function openCreate() {
    setEditingTx(undefined)
    setSheetOpen(true)
  }

  function openEdit(tx: Transaction) {
    setEditingTx(tx)
    setSheetOpen(true)
  }

  const summary  = summaryQ.data
  const currency = summary?.baseCurrency || 'EUR'

  return (
    <div className="mx-auto max-w-6xl px-5 py-8 md:px-8">

      {/* ── Cabecera ──────────────────────────────────────────────────────── */}
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="font-heading text-2xl font-semibold">Gastos e ingresos</h1>

        {/* Botón "Nueva transacción": en móvil solo el icono "+", en ≥sm texto completo.
            Puro CSS — el <span> del texto se oculta bajo el breakpoint sm. */}
        <Button onClick={openCreate} data-testid="btn-nueva-transaccion" aria-label="Nueva transacción">
          <Plus className="h-4 w-4 sm:mr-2" />
          <span className="hidden sm:inline">Nueva transacción</span>
        </Button>
      </div>

      {/* ── Pestañas: Listado / Gráficas ──────────────────────────────────── */}
      <Tabs value={tab} onValueChange={(v) => setTab(v as 'list' | 'charts')}>
        <TabsList className="mb-6">
          <TabsTrigger value="list" data-testid="tab-list">Listado</TabsTrigger>
          <TabsTrigger value="charts" data-testid="tab-charts">Gráficas</TabsTrigger>
        </TabsList>

        {/* ════════════ Pestaña LISTADO ════════════ */}
        <TabsContent value="list">

      {/* Selector de mes/año */}
      <div className="mb-6 flex justify-end">
        <div className="flex items-center gap-2" data-testid="month-selector">
          <Button
            variant="outline" size="icon"
            onClick={prevMonth}
            aria-label="Mes anterior"
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="min-w-[130px] text-center text-sm font-medium">
            {MONTH_NAMES[month - 1]} {year}
          </span>
          <Button
            variant="outline" size="icon"
            onClick={nextMonth}
            aria-label="Mes siguiente"
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* ── KPIs de resumen ───────────────────────────────────────────────── */}
      <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
        {summaryQ.isLoading ? (
          Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-[72px] rounded-lg" />
          ))
        ) : (
          <>
            <SummaryCard
              label="Ingresos"
              amount={summary?.totalIncome ?? 0}
              currency={currency}
              icon={<TrendingUp className="h-4 w-4" />}
              colorClass="text-positive"
            />
            <SummaryCard
              label="Gastos"
              amount={summary?.totalExpense ?? 0}
              currency={currency}
              icon={<TrendingDown className="h-4 w-4" />}
              colorClass="text-negative"
            />
            <SummaryCard
              label="Balance"
              amount={summary?.balance ?? 0}
              currency={currency}
              icon={<Wallet className="h-4 w-4" />}
              colorClass={(summary?.balance ?? 0) >= 0 ? 'text-positive' : 'text-negative'}
            />
          </>
        )}
      </div>

      {/* ── Lista de transacciones ─────────────────────────────────────────── */}

      {/* Cargando — barras neutras válidas en desktop y móvil */}
      {isLoading && (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} data-testid="skeleton-row" className="h-14 rounded-lg" />
          ))}
        </div>
      )}

      {/* Error */}
      {isError && (
        <div
          className="rounded-lg border border-border bg-card py-10 text-center text-sm text-destructive"
          data-testid="error-state"
        >
          Error al cargar las transacciones. Inténtalo de nuevo.
        </div>
      )}

      {/* Vacío */}
      {!isLoading && !isError && data?.items.length === 0 && (
        <div
          className="rounded-lg border border-border bg-card py-10 text-center text-sm text-muted-foreground"
          data-testid="empty-state"
        >
          No hay transacciones en {MONTH_NAMES[month - 1].toLowerCase()} {year}.
        </div>
      )}

      {/* Datos */}
      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          {/* Desktop (md+): tabla. La descripción no es columna: se indica con un icono
              (StickyNote) que aparece solo si existe, con la descripción en el title. */}
          <div className="hidden overflow-hidden rounded-lg border border-border bg-card md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Fecha</TableHead>
                  <TableHead>Tipo</TableHead>
                  <TableHead>Categoría</TableHead>
                  <TableHead className="w-8" />
                  <TableHead className="text-right">Importe</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((tx) => (
                  <TableRow
                    key={tx.idTransaction}
                    data-testid="tx-row"
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => openEdit(tx)}
                  >
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(tx.transactionDate)}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={tx.type === 'Income' ? 'default' : 'outline'}
                        className={
                          tx.type === 'Income'
                            ? 'border-0 bg-positive/10 text-positive hover:bg-positive/20'
                            : 'border-0 bg-negative/10 text-negative hover:bg-negative/20'
                        }
                      >
                        {TRANSACTION_TYPE_LABEL[tx.type]}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-sm">
                      {MAIN_CATEGORY_LABEL[tx.idMainCategory]}
                      {subCatName(tx.idSubCategory) && (
                        <span className="text-muted-foreground">
                          {' · '}{subCatName(tx.idSubCategory)}
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="w-8 text-muted-foreground">
                      {tx.description && (
                        <span title={tx.description} className="inline-flex">
                          <StickyNote className="h-4 w-4" aria-label="Tiene descripción" />
                        </span>
                      )}
                    </TableCell>
                    <TableCell
                      className={`text-right font-medium ${
                        tx.type === 'Income' ? 'text-positive' : 'text-negative'
                      }`}
                    >
                      {tx.type === 'Income' ? '+' : '-'}
                      {formatAmount(tx.originalAmount, tx.originalCurrency)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Móvil (<md): tarjetas de 2 líneas — Fecha | Importe / Categoría · Subcategoría */}
          <div className="flex flex-col gap-2 md:hidden">
            {data.items.map((tx) => (
              <button
                key={tx.idTransaction}
                type="button"
                onClick={() => openEdit(tx)}
                className="flex flex-col gap-1 rounded-lg border border-border bg-card p-3 text-left transition-colors hover:bg-muted/50"
              >
                {/* Línea 1: Fecha · · · Importe */}
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm text-muted-foreground">
                    {formatDate(tx.transactionDate)}
                  </span>
                  <span
                    className={`text-sm font-medium ${
                      tx.type === 'Income' ? 'text-positive' : 'text-negative'
                    }`}
                  >
                    {tx.type === 'Income' ? '+' : '-'}
                    {formatAmount(tx.originalAmount, tx.originalCurrency)}
                  </span>
                </div>
                {/* Línea 2: Categoría · Subcategoría (+ icono si hay descripción) */}
                <div className="flex items-center gap-1.5 text-sm">
                  <span>
                    {MAIN_CATEGORY_LABEL[tx.idMainCategory]}
                    {subCatName(tx.idSubCategory) && (
                      <span className="text-muted-foreground">
                        {' · '}{subCatName(tx.idSubCategory)}
                      </span>
                    )}
                  </span>
                  {tx.description && (
                    <StickyNote
                      className="h-3.5 w-3.5 shrink-0 text-muted-foreground"
                      aria-label="Tiene descripción"
                    />
                  )}
                </div>
              </button>
            ))}
          </div>
        </>
      )}

      {/* ── Paginación ─────────────────────────────────────────────────────── */}
      {data && data.meta.totalPages > 1 && (
        <div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, data.meta.totalCount)} de{' '}
            {data.meta.totalCount}
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline" size="sm"
              onClick={() => setPage((p) => p - 1)}
              disabled={page <= 1}
            >
              <ChevronLeft className="h-4 w-4" /> Anterior
            </Button>
            <Button
              variant="outline" size="sm"
              onClick={() => setPage((p) => p + 1)}
              disabled={page >= data.meta.totalPages}
            >
              Siguiente <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}

        </TabsContent>

        {/* ════════════ Pestaña GRÁFICAS ════════════ */}
        <TabsContent value="charts">

          {/* Conmutador Gastos / Ingresos */}
          <div className="mb-6 inline-flex rounded-lg border border-border p-0.5" role="tablist" aria-label="Tipo de gráfica">
            {(['Expense', 'Income'] as const).map((t) => (
              <button
                key={t}
                type="button"
                role="tab"
                aria-selected={chartType === t}
                onClick={() => setChartType(t)}
                data-testid={`chart-type-${t}`}
                className={`rounded-md px-3 py-1 text-sm font-medium transition-colors ${
                  chartType === t
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                {TRANSACTION_TYPE_LABEL[t]}s
              </button>
            ))}
          </div>

          <p className="mb-4 text-sm text-muted-foreground">
            {chartType === 'Expense' ? 'Gastos' : 'Ingresos'} por categoría — últimos 4 años
            (importes en {currency}).
          </p>

          {chart.isLoading && (
            <Skeleton className="h-[340px] w-full rounded-lg" data-testid="chart-loading" />
          )}

          {chart.isError && (
            <div
              className="rounded-lg border border-border bg-card py-16 text-center text-sm text-destructive"
              data-testid="chart-error"
            >
              Error al cargar los datos de la gráfica. Inténtalo de nuevo.
            </div>
          )}

          {!chart.isLoading && !chart.isError && (
            <CategoryBars
              data={chart.data}
              currency={currency}
              emptyLabel={`No hay ${chartType === 'Expense' ? 'gastos' : 'ingresos'} en los últimos 4 años.`}
            />
          )}

        </TabsContent>
      </Tabs>

      {/* Sheet de creación/edición — montado condicionalmente para que sus hooks
          no se ejecuten cuando está cerrado (evita mocks adicionales en tests). */}
      {sheetOpen && (
        <TransactionSheet
          open={sheetOpen}
          onClose={() => setSheetOpen(false)}
          transaction={editingTx}
        />
      )}

    </div>
  )
}
