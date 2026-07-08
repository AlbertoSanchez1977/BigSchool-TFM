'use client'

import { useState, useMemo } from 'react'
import { use } from 'react'
import Link from 'next/link'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { useRouter } from 'next/navigation'
import {
  ChevronLeft, MoreVertical, Pencil, Plus, StickyNote, Trash2, TrendingUp, TrendingDown, Minus, DollarSign,
} from 'lucide-react'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  usePortfolioDetail, useAddHolding, useUpdateHoldingNotes, useDeleteHolding,
} from '@/hooks/useHoldings'
import { usePerformance, useSellShares } from '@/hooks/usePerformance'
import { useCompanies } from '@/hooks/useCompanies'
import {
  RenamePortfolioModal, DeletePortfolioModal,
} from '@/components/investments/portfolio-action-modals'
import { formatAmount, formatPct, colorPnL, signPnL } from '@/lib/transactions/labels'
import type { HoldingListItem, HoldingPerformance } from '@/types/portfolios'

// ── Schemas Zod ───────────────────────────────────────────────────────────────

const addHoldingSchema = z.object({
  idCompany: z.number({ error: 'Selecciona una empresa' }).int().positive('Selecciona una empresa'),
  shares:    z.number({ error: 'Las acciones deben ser un número' }).positive('Debe ser mayor que cero'),
  buyPrice:  z.number({ error: 'El precio debe ser un número' }).positive('Debe ser mayor que cero'),
  buyDate:   z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Formato de fecha inválido'),
  notes:     z.string().max(500, 'Máximo 500 caracteres').nullable().optional(),
})
type AddHoldingForm = z.infer<typeof addHoldingSchema>

const editNotesSchema = z.object({
  notes: z.string().max(500, 'Máximo 500 caracteres').nullable().optional(),
})
type EditNotesForm = z.infer<typeof editNotesSchema>

// Tipo base para la venta; el max de acciones se refina dinámicamente en el modal
const sellSchemaBase = z.object({
  shares:    z.number({ error: 'Las acciones deben ser un número' }).positive('Debe ser mayor que cero'),
  sellPrice: z.number({ error: 'El precio debe ser un número' }).positive('Debe ser mayor que cero'),
  sellDate:  z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Formato de fecha inválido'),
  notes:     z.string().max(500, 'Máximo 500 caracteres').nullable().optional(),
})
type SellForm = z.infer<typeof sellSchemaBase>

// ── Helpers ───────────────────────────────────────────────────────────────────

function todayISO() {
  return new Date().toISOString().split('T')[0]
}

function Field({
  label, htmlFor, error, children,
}: {
  label: React.ReactNode; htmlFor?: string; error?: string; children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  )
}

// ── Modal: Añadir holding ─────────────────────────────────────────────────────

function AddHoldingModal({
  portfolioId, open, onClose,
}: { portfolioId: number; open: boolean; onClose: () => void }) {
  const addMutation            = useAddHolding(portfolioId)
  const { data: companies = [] } = useCompanies()

  const form = useForm<AddHoldingForm>({
    resolver: zodResolver(addHoldingSchema),
    defaultValues: { buyDate: todayISO(), notes: '' },
  })

  function onSubmit(values: AddHoldingForm) {
    addMutation.mutate(
      {
        idCompany: values.idCompany,
        shares:    values.shares,
        buyPrice:  values.buyPrice,
        buyDate:   values.buyDate,
        notes:     values.notes || null,
      },
      {
        onSuccess: () => { toast.success('Holding añadido'); onClose(); form.reset() },
        onError:   (e) => toast.error(e instanceof Error ? e.message : 'Error al añadir'),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto p-6 sm:max-w-md">
        <DialogHeader className="mb-2">
          <DialogTitle>Añadir holding</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">

          <Field label="Empresa" htmlFor="idCompany"
            error={form.formState.errors.idCompany?.message}>
            <Controller
              control={form.control}
              name="idCompany"
              render={({ field }) => (
                <Select
                  value={field.value != null ? String(field.value) : ''}
                  onValueChange={(v) => field.onChange(Number(v))}
                  // Select es modal por defecto (bloquea scroll + interacción fuera de él);
                  // anidado dentro de un Dialog (también modal) provoca que, al cerrarse el
                  // Select tras elegir, su propio desbloqueo de scroll pise momentáneamente
                  // el bloqueo del Dialog padre — se ve como un parpadeo en móvil. El Dialog
                  // ya bloquea la interacción exterior, así que el Select anidado no necesita
                  // repetirlo.
                  modal={false}
                >
                  <SelectTrigger id="idCompany" className="w-full" data-testid="select-company">
                    <SelectValue placeholder="Selecciona empresa…">
                      {(v: string) => {
                        const c = companies.find((co) => String(co.idCompany) === v)
                        return c ? `${c.ticker} — ${c.name}` : 'Selecciona empresa…'
                      }}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent alignItemWithTrigger={false}>
                    {companies.map((c) => (
                      <SelectItem key={c.idCompany} value={String(c.idCompany)}>
                        <span className="font-mono text-xs text-muted-foreground">{c.ticker}</span>
                        {' '}{c.name}
                        {c.lastPrice != null && (
                          <span className="ml-1 text-xs text-muted-foreground">
                            · {formatAmount(c.lastPrice, c.currency)}
                          </span>
                        )}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Acciones" htmlFor="shares"
              error={form.formState.errors.shares?.message}>
              <Input
                id="shares" type="number" step="0.0001" min="0.0001"
                placeholder="10"
                data-testid="input-shares"
                {...form.register('shares', { valueAsNumber: true })}
              />
            </Field>
            <Field label="Precio compra" htmlFor="buyPrice"
              error={form.formState.errors.buyPrice?.message}>
              <Input
                id="buyPrice" type="number" step="0.01" min="0.01"
                placeholder="0,00"
                data-testid="input-buy-price"
                {...form.register('buyPrice', { valueAsNumber: true })}
              />
            </Field>
          </div>

          <Field label="Fecha de compra" htmlFor="buyDate"
            error={form.formState.errors.buyDate?.message}>
            <Input
              id="buyDate" type="date"
              data-testid="input-buy-date"
              {...form.register('buyDate')}
            />
          </Field>

          <Field label={<>Notas{' '}<span className="text-muted-foreground">(opcional)</span></>}
            htmlFor="notes">
            <Textarea
              id="notes" rows={3} className="resize-none overflow-y-auto"
              placeholder="Contexto de la compra…"
              data-testid="input-notes"
              {...form.register('notes')}
            />
          </Field>

          <Button type="submit" className="w-full" disabled={addMutation.isPending}
            data-testid="btn-add-holding">
            {addMutation.isPending ? 'Añadiendo…' : 'Añadir holding'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Modal: Editar notas ───────────────────────────────────────────────────────

function EditNotesModal({
  portfolioId, holding, open, onClose,
}: { portfolioId: number; holding: HoldingListItem; open: boolean; onClose: () => void }) {
  const updateMutation = useUpdateHoldingNotes(portfolioId)

  const form = useForm<EditNotesForm>({
    resolver: zodResolver(editNotesSchema),
    defaultValues: { notes: holding.notes ?? '' },
  })

  function onSubmit(values: EditNotesForm) {
    updateMutation.mutate(
      { holdingId: holding.idHolding, notes: values.notes || null },
      {
        onSuccess: () => { toast.success('Notas guardadas'); onClose() },
        onError:   (e) => toast.error(e instanceof Error ? e.message : 'Error al guardar'),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="p-6 sm:max-w-sm">
        <DialogHeader className="mb-2">
          <DialogTitle>Notas — {holding.ticker}</DialogTitle>
        </DialogHeader>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <Field label="Notas" htmlFor="edit-notes"
            error={form.formState.errors.notes?.message}>
            <Textarea
              id="edit-notes" rows={4} className="resize-none overflow-y-auto"
              placeholder="Contexto de la posición…"
              data-testid="input-edit-notes"
              {...form.register('notes')}
            />
          </Field>
          <Button type="submit" className="w-full" disabled={updateMutation.isPending}>
            {updateMutation.isPending ? 'Guardando…' : 'Guardar notas'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Modal: Vender holding (FIFO) ──────────────────────────────────────────────

function SellSharesModal({
  portfolioId, holding, open, onClose,
}: { portfolioId: number; holding: HoldingListItem; open: boolean; onClose: () => void }) {
  const sellMutation = useSellShares(portfolioId)

  // Añadir restricción de máximo acciones disponibles al schema base
  const schema = useMemo(
    () => sellSchemaBase.extend({
      shares: z.number({ error: 'Las acciones deben ser un número' })
               .positive('Debe ser mayor que cero')
               .max(holding.openShares, `Máximo ${holding.openShares} acciones disponibles`),
    }),
    [holding.openShares],
  )

  const form = useForm<SellForm>({
    resolver: zodResolver(schema),
    defaultValues: { sellDate: todayISO(), notes: '' },
  })

  function onSubmit(values: SellForm) {
    sellMutation.mutate(
      {
        companyId: holding.idCompany,
        shares:    values.shares,
        sellPrice: values.sellPrice,
        sellDate:  values.sellDate,
        notes:     values.notes || null,
      },
      {
        onSuccess: (result) => {
          const pnl = `${signPnL(result.portfolioRealizedPnL)}${formatAmount(result.portfolioRealizedPnL, result.realizedPnLCurrency)}`
          toast.success(`Venta registrada — PnL realizado: ${pnl}`)
          onClose()
          form.reset()
        },
        onError: (e) => toast.error(e instanceof Error ? e.message : 'Error al registrar la venta'),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="p-6 sm:max-w-sm">
        <DialogHeader className="mb-2">
          <DialogTitle>Vender — {holding.ticker}</DialogTitle>
        </DialogHeader>

        {/* Explicación FIFO — hueco #3 de la spec */}
        <p className="rounded-lg bg-muted px-3 py-2 text-xs text-muted-foreground">
          La venta aplica FIFO: si las acciones a vender superan un lote de compra, se
          consumirán varios lotes automáticamente hasta completar la cantidad indicada.
        </p>

        <form onSubmit={form.handleSubmit(onSubmit)} className="mt-4 space-y-4">

          <Field
            label={`Acciones a vender (máx. ${holding.openShares})`}
            htmlFor="sell-shares"
            error={form.formState.errors.shares?.message}
          >
            <Input
              id="sell-shares" type="number" step="0.0001" min="0.0001"
              max={holding.openShares}
              placeholder="0"
              data-testid="input-sell-shares"
              {...form.register('shares', { valueAsNumber: true })}
            />
          </Field>

          <Field label="Precio de venta" htmlFor="sell-price"
            error={form.formState.errors.sellPrice?.message}>
            <Input
              id="sell-price" type="number" step="0.01" min="0.01"
              placeholder="0,00"
              data-testid="input-sell-price"
              {...form.register('sellPrice', { valueAsNumber: true })}
            />
          </Field>

          <Field label="Fecha de venta" htmlFor="sell-date"
            error={form.formState.errors.sellDate?.message}>
            <Input
              id="sell-date" type="date"
              data-testid="input-sell-date"
              {...form.register('sellDate')}
            />
          </Field>

          <Field label={<>Notas{' '}<span className="text-muted-foreground">(opcional)</span></>}
            htmlFor="sell-notes">
            <Textarea
              id="sell-notes" rows={2} className="resize-none overflow-y-auto"
              placeholder="Motivo de la venta…"
              {...form.register('notes')}
            />
          </Field>

          <Button type="submit" className="w-full" disabled={sellMutation.isPending}
            data-testid="btn-confirm-sell">
            {sellMutation.isPending ? 'Registrando…' : 'Confirmar venta'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Card de holding ───────────────────────────────────────────────────────────

function HoldingCard({
  holding, portfolioId,
}: { holding: HoldingListItem; portfolioId: number }) {
  const [editNotesOpen, setEditNotesOpen] = useState(false)
  const [sellOpen, setSellOpen]           = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const deleteMutation                    = useDeleteHolding(portfolioId)

  const currency = holding.buyBaseCurrency || 'EUR'
  const PnLIcon  = holding.unrealizedPnL >= 0 ? TrendingUp : TrendingDown

  function handleDelete() {
    deleteMutation.mutate(holding.idHolding, {
      onSuccess: () => { toast.success('Holding eliminado'); setConfirmDelete(false) },
      onError:   (e) => toast.error(e instanceof Error ? e.message : 'Error al eliminar'),
    })
  }

  return (
    <>
      <div data-testid="holding-card" className="rounded-lg border border-border bg-card p-4">

        {/* Cabecera: ticker + moneda empresa + acciones abiertas */}
        <div className="mb-3 flex items-start justify-between gap-2">
          <div>
            <span className="font-mono text-sm font-semibold">{holding.ticker}</span>
            <span className="ml-2 text-xs text-muted-foreground">{holding.companyCurrency}</span>
          </div>
          <span className="text-xs text-muted-foreground">
            {holding.openShares} / {holding.shares} acciones
          </span>
        </div>

        {/* KPIs: valor mercado, coste base, PnL no realizado */}
        <div className="mb-3 grid grid-cols-3 gap-2 text-sm">
          <div>
            <p className="text-xs text-muted-foreground">Valor mercado</p>
            <p className="font-medium tabular-nums">{formatAmount(holding.marketValue, currency)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Coste base</p>
            <p className="font-medium tabular-nums">{formatAmount(holding.costBasis, currency)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">No realizado</p>
            <p className={`flex items-center gap-0.5 font-medium tabular-nums ${colorPnL(holding.unrealizedPnL)}`}>
              <PnLIcon className="h-3.5 w-3.5" />
              {signPnL(holding.unrealizedPnL)}{formatAmount(holding.unrealizedPnL, currency)}
            </p>
          </div>
        </div>

        {/* Fecha de compra + nota si existe */}
        <div className="mb-3 flex items-center gap-3 text-xs text-muted-foreground">
          <span>Compra: {holding.buyDate}</span>
          {holding.notes && (
            <span className="flex items-center gap-1">
              <StickyNote className="h-3.5 w-3.5" />
              <span className="line-clamp-1 max-w-[200px]">{holding.notes}</span>
            </span>
          )}
        </div>

        {/* Acciones: editar notas + vender + eliminar */}
        {!confirmDelete ? (
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="outline" size="sm"
              onClick={() => setEditNotesOpen(true)} data-testid="btn-edit-notes">
              <StickyNote className="mr-1.5 h-3.5 w-3.5" />
              {holding.notes ? 'Editar notas' : 'Añadir notas'}
            </Button>
            {holding.openShares > 0 && (
              <Button type="button" variant="outline" size="sm"
                onClick={() => setSellOpen(true)} data-testid="btn-sell-holding">
                <DollarSign className="mr-1.5 h-3.5 w-3.5" />
                Vender
              </Button>
            )}
            <Button type="button" variant="ghost" size="sm"
              className="text-destructive hover:text-destructive"
              onClick={() => setConfirmDelete(true)} data-testid="btn-delete-holding">
              <Trash2 className="mr-1.5 h-3.5 w-3.5" />
              Eliminar
            </Button>
          </div>
        ) : (
          <div className="flex flex-col gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3">
            <p className="text-center text-xs text-destructive">
              ¿Eliminar este holding? Esta acción no se puede deshacer.
            </p>
            <div className="flex gap-2">
              <Button type="button" variant="outline" size="sm" className="flex-1"
                onClick={() => setConfirmDelete(false)} disabled={deleteMutation.isPending}>
                Cancelar
              </Button>
              <Button type="button" variant="destructive" size="sm" className="flex-1"
                onClick={handleDelete} disabled={deleteMutation.isPending}
                data-testid="btn-confirm-delete">
                {deleteMutation.isPending ? 'Eliminando…' : 'Sí, eliminar'}
              </Button>
            </div>
          </div>
        )}
      </div>

      {editNotesOpen && (
        <EditNotesModal portfolioId={portfolioId} holding={holding}
          open={editNotesOpen} onClose={() => setEditNotesOpen(false)} />
      )}
      {sellOpen && (
        <SellSharesModal portfolioId={portfolioId} holding={holding}
          open={sellOpen} onClose={() => setSellOpen(false)} />
      )}
    </>
  )
}

// ── Tab: Performance ──────────────────────────────────────────────────────────

function PerformanceTab({ portfolioId, enabled }: { portfolioId: number; enabled: boolean }) {
  const { data: perf, isLoading, isError } = usePerformance(portfolioId, { enabled })

  if (isLoading) return (
    <div className="flex flex-col gap-3">
      {Array.from({ length: 4 }).map((_, i) => (
        <Skeleton key={i} className="h-16 rounded-lg" />
      ))}
    </div>
  )

  if (isError) return (
    <div className="rounded-lg border border-border bg-card py-10 text-center text-sm text-destructive">
      Error al cargar el performance. Inténtalo de nuevo.
    </div>
  )

  if (!perf) return null

  const cur = perf.baseCurrency

  const kpis = [
    { label: 'Valor mercado', value: formatAmount(perf.marketValue, cur),                                            color: '' },
    { label: 'Coste base',    value: formatAmount(perf.costBasis, cur),                                              color: '' },
    { label: 'No realizado',  value: `${signPnL(perf.unrealizedPnL)}${formatAmount(perf.unrealizedPnL, cur)}`,      color: colorPnL(perf.unrealizedPnL) },
    { label: 'Realizado',     value: `${signPnL(perf.realizedPnL)}${formatAmount(perf.realizedPnL, cur)}`,          color: colorPnL(perf.realizedPnL) },
    { label: 'Total PnL',     value: `${signPnL(perf.totalPnL)}${formatAmount(perf.totalPnL, cur)}`,                color: colorPnL(perf.totalPnL) },
    { label: 'Rentabilidad',  value: `${signPnL(perf.returnPct)}${formatPct(perf.returnPct, 2)}`,                    color: colorPnL(perf.returnPct) },
  ]

  return (
    <div>
      {/* KPIs de cartera */}
      <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-3">
        {kpis.map(({ label, value, color }) => (
          <div key={label} className="rounded-lg border border-border bg-card px-4 py-3">
            <p className="text-xs text-muted-foreground">{label}</p>
            <p className={`text-sm font-semibold tabular-nums ${color}`}>{value}</p>
          </div>
        ))}
      </div>

      {/* Cards por holding — 3 columnas: base | original | mercado+% */}
      {perf.holdings.length > 0 ? (
        <div className="flex flex-col gap-3">
          {perf.holdings.map((h: HoldingPerformance) => {
            const origCur = h.buyOriginalCurrency

            return (
              <div key={h.idHolding} className="rounded-lg border border-border bg-card p-4">
                {/* Cabecera */}
                <div className="mb-3 flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-sm font-semibold">{h.ticker}</span>
                    {origCur !== cur && (
                      <span className="text-xs text-muted-foreground">{origCur}</span>
                    )}
                  </div>
                  <span className="text-xs text-muted-foreground">{h.openShares} acciones</span>
                </div>

                {/* Grid 3 col × 3 filas */}
                <div className="grid grid-cols-3 gap-x-3 gap-y-2 text-sm">
                  <div>
                    <p className="text-xs text-muted-foreground">Coste base</p>
                    <p className="font-medium tabular-nums">{formatAmount(h.costBasis, cur)}</p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Coste original</p>
                    <p className="font-medium tabular-nums">
                      {formatAmount(h.costBasisOriginal, origCur)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Valor mercado</p>
                    <p className="font-medium tabular-nums">{formatAmount(h.marketValue, cur)}</p>
                  </div>

                  <div>
                    <p className="text-xs text-muted-foreground">Valor mercado original</p>
                    <p className="font-medium tabular-nums">
                      {formatAmount(h.marketValueOriginal, origCur)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">PnL base</p>
                    <p className={`font-medium tabular-nums ${colorPnL(h.unrealizedPnL)}`}>
                      {signPnL(h.unrealizedPnL)}{formatAmount(h.unrealizedPnL, cur)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">PnL original</p>
                    <p className={`font-medium tabular-nums ${colorPnL(h.unrealizedPnLOriginal)}`}>
                      {signPnL(h.unrealizedPnLOriginal)}{formatAmount(h.unrealizedPnLOriginal, origCur)}
                    </p>
                  </div>

                  <div>
                    <p className="text-xs text-muted-foreground">Rentabilidad</p>
                    <p className={`font-medium tabular-nums ${colorPnL(h.unrealizedPnLPct)}`}>
                      {signPnL(h.unrealizedPnLPct)}{formatPct(h.unrealizedPnLPct, 2)}
                    </p>
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      ) : (
        <p className="text-center text-sm text-muted-foreground">
          No hay holdings con posición abierta.
        </p>
      )}
    </div>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────
// RenamePortfolioModal / DeletePortfolioModal viven en
// components/investments/portfolio-action-modals.tsx (compartidos con el listado).

export default function HoldingsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params)
  const portfolioId = Number(id)
  const router = useRouter()

  const [addOpen, setAddOpen]       = useState(false)
  const [renameOpen, setRenameOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [tab, setTab]               = useState<'holdings' | 'performance'>('holdings')

  const { data: portfolio, isLoading, isError } = usePortfolioDetail(portfolioId)

  const currency = portfolio?.realizedPnLCurrency || 'EUR'

  return (
    <div className="mx-auto max-w-6xl px-5 py-8 md:px-8">

      {/* ── Breadcrumb-header ─────────────────────────────────────────────── */}
      {/* Patrón maestro-detalle: <Link> fijo a /investments, no router.back() */}
      <div className="mb-6 flex items-center justify-between gap-4">
        <div className="flex min-w-0 flex-1 items-center gap-2">
          <Link
            href="/investments"
            className="flex shrink-0 items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
            aria-label="Volver a Inversiones"
            data-testid="back-link"
          >
            <ChevronLeft className="h-4 w-4" />
            Inversiones
          </Link>
          {portfolio && (
            <>
              <span className="shrink-0 text-muted-foreground/50">/</span>
              <h1 className="truncate font-heading text-lg font-semibold">{portfolio.name}</h1>
            </>
          )}
          {isLoading && <Skeleton className="h-6 w-32 shrink-0" />}
        </div>

        <div className="flex shrink-0 items-center gap-2">
          {tab === 'holdings' && (
            <Button
              onClick={() => setAddOpen(true)}
              data-testid="btn-add-holding"
              aria-label="Añadir holding"
            >
              <Plus className="h-4 w-4 sm:mr-2" />
              <span className="hidden sm:inline">Añadir holding</span>
            </Button>
          )}

          {portfolio && (
            <DropdownMenu>
              <DropdownMenuTrigger
                aria-label="Más acciones"
                data-testid="btn-portfolio-menu"
                className={buttonVariants({ variant: 'outline', size: 'icon' })}
              >
                <MoreVertical className="h-4 w-4" />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={() => setRenameOpen(true)} data-testid="menu-rename-portfolio">
                  <Pencil className="mr-2 h-4 w-4" aria-hidden="true" />
                  Renombrar
                </DropdownMenuItem>
                <DropdownMenuItem
                  variant="destructive"
                  onClick={() => setDeleteOpen(true)}
                  data-testid="menu-delete-portfolio"
                >
                  <Trash2 className="mr-2 h-4 w-4" aria-hidden="true" />
                  Eliminar
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </div>

      {/* ── Tabs: Holdings | Performance ─────────────────────────────────── */}
      <Tabs value={tab} onValueChange={(v) => setTab(v as typeof tab)}>
        <TabsList>
          <TabsTrigger value="holdings">Holdings</TabsTrigger>
          <TabsTrigger value="performance">Performance</TabsTrigger>
        </TabsList>

        {/* ── Tab Holdings ──────────────────────────────────────────────── */}
        <TabsContent value="holdings" className="mt-4">

          {/* KPI de cartera */}
          {portfolio && (
            <div className="mb-6 flex flex-wrap items-center gap-4 rounded-lg border border-border bg-card px-4 py-3">
              <div>
                <p className="text-xs text-muted-foreground">PnL realizado</p>
                <p className={`text-sm font-semibold tabular-nums ${colorPnL(portfolio.realizedPnL)}`}>
                  {signPnL(portfolio.realizedPnL)}{formatAmount(portfolio.realizedPnL, currency)}
                </p>
              </div>
              <Minus className="hidden h-3 w-3 text-muted-foreground/30 sm:block" />
              <p className="text-xs text-muted-foreground">
                {portfolio.holdings.length} holding{portfolio.holdings.length !== 1 ? 's' : ''}
              </p>
            </div>
          )}

          {/* Estados */}
          {isLoading && (
            <div className="flex flex-col gap-3" data-testid="loading-state">
              {Array.from({ length: 3 }).map((_, i) => (
                <Skeleton key={i} className="h-[160px] rounded-lg" />
              ))}
            </div>
          )}

          {isError && (
            <div
              className="rounded-lg border border-border bg-card py-10 text-center text-sm text-destructive"
              data-testid="error-state"
            >
              Error al cargar los holdings. Inténtalo de nuevo.
            </div>
          )}

          {!isLoading && !isError && portfolio?.holdings.length === 0 && (
            <div
              className="rounded-lg border border-border bg-card py-12 text-center"
              data-testid="empty-state"
            >
              <TrendingUp className="mx-auto mb-3 h-8 w-8 text-muted-foreground/40" />
              <p className="text-sm text-muted-foreground">No hay holdings en esta cartera.</p>
              <Button variant="outline" size="sm" className="mt-4" onClick={() => setAddOpen(true)}>
                Añadir el primer holding
              </Button>
            </div>
          )}

          {!isLoading && !isError && portfolio && portfolio.holdings.length > 0 && (
            <div className="flex flex-col gap-3">
              {portfolio.holdings.map((h) => (
                <HoldingCard key={h.idHolding} holding={h} portfolioId={portfolioId} />
              ))}
            </div>
          )}
        </TabsContent>

        {/* ── Tab Performance ───────────────────────────────────────────── */}
        <TabsContent value="performance" className="mt-4">
          <PerformanceTab portfolioId={portfolioId} enabled={tab === 'performance'} />
        </TabsContent>
      </Tabs>

      {/* Modales — montados condicionalmente */}
      {addOpen && (
        <AddHoldingModal
          portfolioId={portfolioId}
          open={addOpen}
          onClose={() => setAddOpen(false)}
        />
      )}
      {renameOpen && portfolio && (
        <RenamePortfolioModal
          portfolioId={portfolioId}
          currentName={portfolio.name}
          open={renameOpen}
          onClose={() => setRenameOpen(false)}
        />
      )}
      {deleteOpen && (
        <DeletePortfolioModal
          portfolioId={portfolioId}
          open={deleteOpen}
          onClose={() => setDeleteOpen(false)}
          onDeleted={() => router.push('/investments')}
        />
      )}
    </div>
  )
}
