'use client'

import { use, useState } from 'react'
import Link from 'next/link'
import { ChevronLeft, Plus } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Pagination } from '@/components/ui/pagination'
import { ValuationSeriesChart } from '@/components/market/valuation-series-chart'
import { useCompanyDetail } from '@/hooks/useCompanyDetail'
import { useValuations, useCreateValuation } from '@/hooks/useValuations'
import { formatAmount, formatDate } from '@/lib/transactions/labels'
import { SECTOR_LABEL } from '@/lib/investments/labels'
import { DEFAULT_PAGE_SIZE } from '@/types/pagination'
import { todayISO, isNotFuture } from '@/lib/dates'
import type { Sector } from '@/types/enums'

// ── Schema Zod del formulario "Nueva valoración" ──────────────────────────────
// Sin currency: el backend la hereda de la empresa (Company.Currency), no viaja en el body.
const createValuationSchema = z.object({
  price:  z.number({ error: 'El precio debe ser un número' }).positive('Debe ser mayor que cero'),
  date:   z.string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Formato de fecha inválido')
    .refine(isNotFuture, 'La fecha no puede ser futura'),
  source: z.string().max(100, 'Máximo 100 caracteres').nullable().optional(),
})
type CreateValuationForm = z.infer<typeof createValuationSchema>

// ── Modal de creación ─────────────────────────────────────────────────────────

function CreateValuationModal({
  companyId, open, onClose,
}: { companyId: number; open: boolean; onClose: () => void }) {
  const createMutation = useCreateValuation(companyId)

  const form = useForm<CreateValuationForm>({
    resolver: zodResolver(createValuationSchema),
    defaultValues: { date: todayISO(), source: '' },
  })

  function onSubmit(values: CreateValuationForm) {
    createMutation.mutate(
      { price: values.price, date: values.date, source: values.source || null },
      {
        onSuccess: () => {
          toast.success('Valoración añadida')
          onClose()
          form.reset({ date: todayISO(), source: '' })
        },
        onError: (e) => toast.error(e instanceof Error ? e.message : 'Error al añadir la valoración'),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="p-6 sm:max-w-sm">
        <DialogHeader className="mb-4">
          <DialogTitle>Nueva valoración</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="valuation-price">Precio</Label>
            <Input
              id="valuation-price" type="number" step="0.01" min="0.01"
              placeholder="0,00"
              data-testid="input-valuation-price"
              {...form.register('price', { valueAsNumber: true })}
            />
            {form.formState.errors.price && (
              <p className="text-xs text-destructive">{form.formState.errors.price.message}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="valuation-date">Fecha</Label>
            <Input
              id="valuation-date" type="date" max={todayISO()}
              data-testid="input-valuation-date"
              {...form.register('date')}
            />
            {form.formState.errors.date && (
              <p className="text-xs text-destructive">{form.formState.errors.date.message}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="valuation-source">
              Origen <span className="text-muted-foreground">(opcional)</span>
            </Label>
            <Input
              id="valuation-source"
              placeholder="Ej: Cierre de mercado"
              data-testid="input-valuation-source"
              {...form.register('source')}
            />
            {form.formState.errors.source && (
              <p className="text-xs text-destructive">{form.formState.errors.source.message}</p>
            )}
          </div>

          <Button
            type="submit"
            className="w-full"
            disabled={createMutation.isPending}
            data-testid="btn-create-valuation"
          >
            {createMutation.isPending ? 'Guardando…' : 'Añadir valoración'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function CompanyDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params)
  const companyId = Number(id)

  const { data: company, isLoading, isError } = useCompanyDetail(companyId)

  const [modalOpen, setModalOpen] = useState(false)
  const [valPage, setValPage] = useState(1)
  const { data: valuations, isLoading: valLoading, isError: valError } =
    useValuations(companyId, valPage, DEFAULT_PAGE_SIZE)

  return (
    <div className="mx-auto max-w-4xl px-5 py-8 md:px-8">

      {/* ── Breadcrumb-header ─────────────────────────────────────────────── */}
      <div className="mb-6 flex items-center gap-2">
        <Link
          href="/market"
          className="flex shrink-0 items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
          aria-label="Volver a Mercado"
          data-testid="back-link"
        >
          <ChevronLeft className="h-4 w-4" />
          Mercado
        </Link>
        {company && (
          <>
            <span className="text-muted-foreground/50">/</span>
            <h1 className="truncate font-heading text-lg font-semibold">{company.ticker}</h1>
          </>
        )}
        {isLoading && <Skeleton className="h-6 w-32" />}
      </div>

      {/* ── Estados ───────────────────────────────────────────────────────── */}
      {isLoading && (
        <div className="flex flex-col gap-3" data-testid="loading-state">
          <Skeleton className="h-24 rounded-lg" />
        </div>
      )}

      {isError && (
        <div
          className="rounded-lg border border-border bg-card py-10 text-center text-sm text-destructive"
          data-testid="error-state"
        >
          Error al cargar la empresa. Inténtalo de nuevo.
        </div>
      )}

      {/* ── Cabecera de datos ─────────────────────────────────────────────── */}
      {/* Sin acciones de editar/borrar: el backend no expone PUT/DELETE /companies/{id}
          (deuda documentada — spec 011 §6). No se pintan botones muertos. */}
      {!isLoading && !isError && company && (
        <div className="rounded-lg border border-border bg-card p-4">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-base font-semibold">{company.ticker}</span>
            <span className="text-base font-medium">{company.name}</span>
          </div>
          <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div>
              <p className="text-xs text-muted-foreground">Sector</p>
              <p className="text-sm font-medium">
                {company.sector ? SECTOR_LABEL[company.sector as Sector] ?? company.sector : '—'}
              </p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Bolsa</p>
              <p className="text-sm font-medium">{company.market ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Moneda</p>
              <p className="text-sm font-medium">{company.currency}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Último precio</p>
              <p className="text-sm font-semibold tabular-nums">
                {company.lastPrice != null ? formatAmount(company.lastPrice, company.currency) : '—'}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* ── Serie de cotización + valoraciones ──────────────────────────────── */}
      {!isLoading && !isError && company && (
        <div className="mt-6 flex flex-col gap-6">
          <ValuationSeriesChart companyId={companyId} />

          {/* Sin ver/borrar valoración individual: el backend no expone GET{id} ni DELETE
              (deuda documentada, spec 011 §6) — solo listado + alta. */}
          <div className="rounded-lg border border-border bg-card p-4">
            <div className="mb-4 flex items-center justify-between gap-4">
              <h2 className="text-base font-semibold">Valoraciones</h2>
              <Button
                size="sm"
                onClick={() => setModalOpen(true)}
                data-testid="btn-nueva-valoracion"
                aria-label="Nueva valoración"
              >
                <Plus className="h-4 w-4 sm:mr-2" />
                <span className="hidden sm:inline">Nueva valoración</span>
              </Button>
            </div>

            {valLoading && (
              <div className="flex flex-col gap-2" data-testid="valuations-loading">
                {Array.from({ length: 3 }).map((_, i) => (
                  <Skeleton key={i} className="h-12 rounded-lg" />
                ))}
              </div>
            )}

            {valError && (
              <div className="py-10 text-center text-sm text-destructive" data-testid="valuations-error">
                Error al cargar las valoraciones.
              </div>
            )}

            {!valLoading && !valError && valuations?.items.length === 0 && (
              <div className="py-10 text-center text-sm text-muted-foreground" data-testid="valuations-empty">
                Aún no hay valoraciones registradas.
              </div>
            )}

            {!valLoading && !valError && valuations && valuations.items.length > 0 && (
              <>
                {/* Desktop (md+): tabla */}
                <div className="hidden overflow-hidden rounded-lg border border-border md:block">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Fecha</TableHead>
                        <TableHead>Origen</TableHead>
                        <TableHead className="text-right">Precio</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {valuations.items.map((v) => (
                        <TableRow key={v.idValuation} data-testid="valuation-row">
                          <TableCell className="text-sm text-muted-foreground">{formatDate(v.date)}</TableCell>
                          <TableCell className="text-sm text-muted-foreground">{v.source ?? '—'}</TableCell>
                          <TableCell className="text-right text-sm font-medium tabular-nums">
                            {formatAmount(v.price, v.priceCurrency)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>

                {/* Móvil (<md): tarjetas de 2 líneas */}
                <div className="flex flex-col gap-2 md:hidden">
                  {valuations.items.map((v) => (
                    <div
                      key={v.idValuation}
                      data-testid="valuation-row"
                      className="flex items-center justify-between gap-2 rounded-lg border border-border p-3"
                    >
                      <div>
                        <p className="text-sm text-muted-foreground">{formatDate(v.date)}</p>
                        {v.source && <p className="text-xs text-muted-foreground">{v.source}</p>}
                      </div>
                      <span className="text-sm font-medium tabular-nums">
                        {formatAmount(v.price, v.priceCurrency)}
                      </span>
                    </div>
                  ))}
                </div>

                <Pagination
                  page={valPage}
                  pageSize={DEFAULT_PAGE_SIZE}
                  totalCount={valuations.meta.totalCount}
                  totalPages={valuations.meta.totalPages}
                  onPageChange={setValPage}
                />
              </>
            )}
          </div>
        </div>
      )}

      {modalOpen && (
        <CreateValuationModal
          companyId={companyId}
          open={modalOpen}
          onClose={() => setModalOpen(false)}
        />
      )}
    </div>
  )
}
