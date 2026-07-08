'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { Pencil, Plus, TrendingUp, ChevronRight, Trash2 } from 'lucide-react'
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
import { Pagination } from '@/components/ui/pagination'
import {
  RenamePortfolioModal, DeletePortfolioModal,
} from '@/components/investments/portfolio-action-modals'
import { usePortfolios, useCreatePortfolio } from '@/hooks/usePortfolios'
import { formatAmount } from '@/lib/transactions/labels'
import type { PortfolioListItem } from '@/types/portfolios'

// ── Schema Zod del formulario "Crear cartera" ─────────────────────────────────
// Solo requiere nombre; el backend no expone currency en la creación.
const createPortfolioSchema = z.object({
  name: z.string().min(1, 'El nombre es obligatorio').max(100, 'Máximo 100 caracteres'),
})
type CreatePortfolioForm = z.infer<typeof createPortfolioSchema>

// ── Modal de creación ─────────────────────────────────────────────────────────
// Sigue el patrón de modal centrado documentado en docs/03-frontend-design.md §7.

function CreatePortfolioModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const createMutation = useCreatePortfolio()
  const router = useRouter()

  const form = useForm<CreatePortfolioForm>({
    resolver: zodResolver(createPortfolioSchema),
    defaultValues: { name: '' },
  })

  function onSubmit(values: CreatePortfolioForm) {
    createMutation.mutate(values, {
      onSuccess: (portfolio) => {
        toast.success('Cartera creada')
        onClose()
        // Navega directamente a la nueva cartera para que el usuario pueda añadir holdings.
        router.push(`/investments/${portfolio.idPortfolio}`)
      },
      onError: (e) => toast.error(e instanceof Error ? e.message : 'Error al crear la cartera'),
    })
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="p-6 sm:max-w-sm">
        <DialogHeader className="mb-4">
          <DialogTitle>Nueva cartera</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="portfolio-name">Nombre</Label>
            <Input
              id="portfolio-name"
              placeholder="Ej: Cartera principal"
              data-testid="input-portfolio-name"
              {...form.register('name')}
            />
            {form.formState.errors.name && (
              <p className="text-xs text-destructive">{form.formState.errors.name.message}</p>
            )}
          </div>

          <Button
            type="submit"
            className="w-full"
            disabled={createMutation.isPending}
            data-testid="btn-create-portfolio"
          >
            {createMutation.isPending ? 'Creando…' : 'Crear cartera'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Card de cartera (alargada, horizontal) ───────────────────────────────────
// Muestra los KPIs principales y actúa como enlace a la vista de holdings.
// No es un <button> (como antes) porque ahora aloja botones propios (editar/borrar):
// un <button> dentro de otro <button> es HTML inválido y el navegador lo "arregla"
// rompiendo el anidado, así que el contenedor pasa a ser un <div role="button"> con
// soporte de teclado (Enter/Espacio) para no perder accesibilidad.

function PortfolioCard({ portfolio }: { portfolio: PortfolioListItem }) {
  const router = useRouter()
  const [renameOpen, setRenameOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const currency = portfolio.realizedPnLCurrency || 'EUR'

  const pnlClass = (v: number) => (v >= 0 ? 'text-positive' : 'text-negative')
  const pnlSign  = (v: number) => (v >= 0 ? '+' : '')

  function goToDetail() {
    router.push(`/investments/${portfolio.idPortfolio}`)
  }

  return (
    <>
      <div
        role="button"
        tabIndex={0}
        data-testid="portfolio-card"
        onClick={goToDetail}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); goToDetail() }
        }}
        className="flex w-full cursor-pointer items-center justify-between gap-4 rounded-lg border border-border bg-card px-5 py-4 text-left transition-colors hover:bg-muted/50"
      >
        {/* Nombre e icono */}
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10">
            <TrendingUp className="h-4 w-4 text-primary" />
          </span>
          <span className="truncate font-medium">{portfolio.name}</span>
        </div>

        {/* KPIs: valor de mercado + PnLs */}
        <div className="flex items-center gap-6 text-right">
          {/* Valor de mercado */}
          <div className="hidden sm:block">
            <p className="text-xs text-muted-foreground">Valor mercado</p>
            <p className="text-sm font-semibold tabular-nums">
              {formatAmount(portfolio.marketValue, currency)}
            </p>
          </div>

          {/* PnL no realizado */}
          <div className="hidden md:block">
            <p className="text-xs text-muted-foreground">No realizado</p>
            <p className={`text-sm font-medium tabular-nums ${pnlClass(portfolio.unrealizedPnL)}`}>
              {pnlSign(portfolio.unrealizedPnL)}{formatAmount(portfolio.unrealizedPnL, currency)}
            </p>
          </div>

          {/* PnL total */}
          <div>
            <p className="text-xs text-muted-foreground">PnL total</p>
            <p className={`text-sm font-medium tabular-nums ${pnlClass(portfolio.totalPnL)}`}>
              {pnlSign(portfolio.totalPnL)}{formatAmount(portfolio.totalPnL, currency)}
            </p>
          </div>

          {/* Editar / Borrar — stopPropagation para no disparar la navegación de la card */}
          <div className="flex items-center gap-1">
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label="Renombrar cartera"
              data-testid="btn-rename-portfolio"
              onClick={(e) => { e.stopPropagation(); setRenameOpen(true) }}
            >
              <Pencil className="h-3.5 w-3.5" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label="Eliminar cartera"
              data-testid="btn-delete-portfolio"
              className="text-destructive hover:text-destructive"
              onClick={(e) => { e.stopPropagation(); setDeleteOpen(true) }}
            >
              <Trash2 className="h-3.5 w-3.5" />
            </Button>
          </div>

          <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
        </div>
      </div>

      {renameOpen && (
        <RenamePortfolioModal
          portfolioId={portfolio.idPortfolio}
          currentName={portfolio.name}
          open={renameOpen}
          onClose={() => setRenameOpen(false)}
        />
      )}
      {deleteOpen && (
        <DeletePortfolioModal
          portfolioId={portfolio.idPortfolio}
          open={deleteOpen}
          onClose={() => setDeleteOpen(false)}
        />
      )}
    </>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

const PAGE_SIZE = 20

export default function InvestmentsPage() {
  const [modalOpen, setModalOpen] = useState(false)
  const [page, setPage] = useState(1)
  const { data, isLoading, isError } = usePortfolios({ page, pageSize: PAGE_SIZE })
  const portfolios = data?.items

  return (
    <div className="mx-auto max-w-6xl px-5 py-8 md:px-8">

      {/* Cabecera */}
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="font-heading text-2xl font-semibold">Inversiones</h1>
        <Button onClick={() => setModalOpen(true)} data-testid="btn-nueva-cartera" aria-label="Nueva cartera">
          <Plus className="h-4 w-4 sm:mr-2" />
          <span className="hidden sm:inline">Nueva cartera</span>
        </Button>
      </div>

      {/* Cargando */}
      {isLoading && (
        <div className="flex flex-col gap-3" data-testid="loading-state">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-[68px] rounded-lg" />
          ))}
        </div>
      )}

      {/* Error */}
      {isError && (
        <div
          className="rounded-lg border border-border bg-card py-10 text-center text-sm text-destructive"
          data-testid="error-state"
        >
          Error al cargar las carteras. Inténtalo de nuevo.
        </div>
      )}

      {/* Vacío */}
      {!isLoading && !isError && portfolios?.length === 0 && (
        <div
          className="rounded-lg border border-border bg-card py-12 text-center"
          data-testid="empty-state"
        >
          <TrendingUp className="mx-auto mb-3 h-8 w-8 text-muted-foreground/40" />
          <p className="text-sm text-muted-foreground">No tienes carteras aún.</p>
          <Button
            variant="outline"
            size="sm"
            className="mt-4"
            onClick={() => setModalOpen(true)}
          >
            Crear mi primera cartera
          </Button>
        </div>
      )}

      {/* Lista de cards */}
      {!isLoading && !isError && portfolios && portfolios.length > 0 && (
        <>
          <div className="flex flex-col gap-3">
            {portfolios.map((p) => (
              <PortfolioCard key={p.idPortfolio} portfolio={p} />
            ))}
          </div>
          {data && (
            <Pagination
              page={page}
              pageSize={PAGE_SIZE}
              totalCount={data.meta.totalCount}
              totalPages={data.meta.totalPages}
              onPageChange={setPage}
            />
          )}
        </>
      )}

      {/* Modal de creación — montado condicionalmente */}
      {modalOpen && (
        <CreatePortfolioModal
          open={modalOpen}
          onClose={() => setModalOpen(false)}
        />
      )}

    </div>
  )
}
