'use client'

import { useState } from 'react'
import { ChevronLeft, ChevronRight, Plus, TrendingDown, TrendingUp, Wallet } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { TransactionSheet } from '@/components/transactions/transaction-sheet'
import { useTransactions } from '@/hooks/useTransactions'
import { useSummary } from '@/hooks/useSummary'
import {
  TRANSACTION_TYPE_LABEL, MAIN_CATEGORY_LABEL, formatAmount, formatDate,
} from '@/lib/transactions/labels'
import type { Transaction } from '@/types/transactions'

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

function SkeletonRow() {
  return (
    <TableRow data-testid="skeleton-row">
      {[1, 2, 3, 4, 5].map((i) => (
        <TableCell key={i}><Skeleton className="h-4 w-full" /></TableCell>
      ))}
    </TableRow>
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

  const { from, to } = monthRange(year, month)

  const { data, isLoading, isError } = useTransactions({ from, to, page, pageSize })
  const summaryQ = useSummary(from, to)

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
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="font-heading text-2xl font-semibold">Gastos e ingresos</h1>

        <div className="flex items-center gap-3">
          {/* Selector de mes/año */}
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

          {/* Botón "Nueva transacción": en móvil solo el icono "+", en ≥sm texto completo.
              Puro CSS — el <span> del texto se oculta bajo el breakpoint sm. */}
          <Button onClick={openCreate} data-testid="btn-nueva-transaccion" aria-label="Nueva transacción">
            <Plus className="h-4 w-4 sm:mr-2" />
            <span className="hidden sm:inline">Nueva transacción</span>
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

      {/* ── Tabla de transacciones ─────────────────────────────────────────── */}
      <div className="rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Fecha</TableHead>
              <TableHead>Tipo</TableHead>
              <TableHead>Categoría</TableHead>
              <TableHead>Descripción</TableHead>
              <TableHead className="text-right">Importe</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>

            {/* Cargando */}
            {isLoading && Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)}

            {/* Error */}
            {isError && (
              <TableRow>
                <TableCell colSpan={5}>
                  <div
                    className="py-10 text-center text-sm text-destructive"
                    data-testid="error-state"
                  >
                    Error al cargar las transacciones. Inténtalo de nuevo.
                  </div>
                </TableCell>
              </TableRow>
            )}

            {/* Vacío */}
            {!isLoading && !isError && data?.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={5}>
                  <div
                    className="py-10 text-center text-sm text-muted-foreground"
                    data-testid="empty-state"
                  >
                    No hay transacciones en {MONTH_NAMES[month - 1].toLowerCase()} {year}.
                  </div>
                </TableCell>
              </TableRow>
            )}

            {/* Datos — fila clickable para editar */}
            {!isLoading && !isError && data?.items.map((tx) => (
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
                </TableCell>
                <TableCell
                  className="max-w-[200px] truncate text-sm"
                  title={tx.description ?? ''}
                >
                  {tx.description ?? '—'}
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
