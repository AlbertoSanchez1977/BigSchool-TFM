'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { Building2, ChevronRight, Plus } from 'lucide-react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { Pagination } from '@/components/ui/pagination'
import { useCompaniesList } from '@/hooks/useCompaniesList'
import { useCreateCompany } from '@/hooks/useCreateCompany'
import { formatAmount } from '@/lib/transactions/labels'
import { SECTOR_LABEL } from '@/lib/investments/labels'
import { CURRENCIES, SECTORS, MARKETS } from '@/types/enums'
import { DEFAULT_PAGE_SIZE } from '@/types/pagination'
import type { CompanyListItem } from '@/types/companies'

// ── Schema Zod del formulario "Nueva empresa" ─────────────────────────────────
// sector/market son opcionales en el backend (Sector?/Market?); currency es obligatoria.
const NONE = '__none__'

const createCompanySchema = z.object({
  name: z.string().min(1, 'El nombre es obligatorio').max(200, 'Máximo 200 caracteres'),
  ticker: z.string().min(1, 'El ticker es obligatorio').max(10, 'Máximo 10 caracteres'),
  sector: z.string(), // NONE o un valor de SECTORS — se traduce a null/Sector al enviar
  market: z.string(), // NONE o un valor de MARKETS
  currency: z.enum(CURRENCIES, { message: 'Selecciona una moneda' }),
})
type CreateCompanyForm = z.infer<typeof createCompanySchema>

// ── Modal de creación ─────────────────────────────────────────────────────────

function CreateCompanyModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const createMutation = useCreateCompany()
  const router = useRouter()

  const form = useForm<CreateCompanyForm>({
    resolver: zodResolver(createCompanySchema),
    defaultValues: { name: '', ticker: '', sector: NONE, market: NONE, currency: 'EUR' },
  })

  function onSubmit(values: CreateCompanyForm) {
    createMutation.mutate(
      {
        name: values.name,
        ticker: values.ticker.toUpperCase(),
        sector: values.sector === NONE ? null : (values.sector as (typeof SECTORS)[number]),
        market: values.market === NONE ? null : (values.market as (typeof MARKETS)[number]),
        currency: values.currency,
      },
      {
        onSuccess: (company) => {
          toast.success('Empresa creada')
          onClose()
          form.reset()
          router.push(`/market/${company.idCompany}`)
        },
        onError: (e) => toast.error(e instanceof Error ? e.message : 'Error al crear la empresa'),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto p-6 sm:max-w-md">
        <DialogHeader className="mb-4">
          <DialogTitle>Nueva empresa</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="company-name">Nombre</Label>
            <Input
              id="company-name"
              placeholder="Ej: Apple Inc."
              data-testid="input-company-name"
              {...form.register('name')}
            />
            {form.formState.errors.name && (
              <p className="text-xs text-destructive">{form.formState.errors.name.message}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="company-ticker">Ticker</Label>
            <Input
              id="company-ticker"
              placeholder="Ej: AAPL"
              maxLength={10}
              className="uppercase"
              data-testid="input-company-ticker"
              {...form.register('ticker')}
            />
            {form.formState.errors.ticker && (
              <p className="text-xs text-destructive">{form.formState.errors.ticker.message}</p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="company-sector">Sector</Label>
              <Controller
                control={form.control}
                name="sector"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange} modal={false}>
                    <SelectTrigger id="company-sector" className="w-full" data-testid="select-sector">
                      <SelectValue>
                        {(v: string) => (v === NONE ? 'Ninguno' : SECTOR_LABEL[v as (typeof SECTORS)[number]])}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent alignItemWithTrigger={false}>
                      <SelectItem value={NONE}>Ninguno</SelectItem>
                      {SECTORS.map((s) => (
                        <SelectItem key={s} value={s}>{SECTOR_LABEL[s]}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="company-market">Bolsa</Label>
              <Controller
                control={form.control}
                name="market"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange} modal={false}>
                    <SelectTrigger id="company-market" className="w-full" data-testid="select-market">
                      <SelectValue>{(v: string) => (v === NONE ? 'Ninguna' : v)}</SelectValue>
                    </SelectTrigger>
                    <SelectContent alignItemWithTrigger={false}>
                      <SelectItem value={NONE}>Ninguna</SelectItem>
                      {MARKETS.map((m) => (
                        <SelectItem key={m} value={m}>{m}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="company-currency">Moneda</Label>
            <Controller
              control={form.control}
              name="currency"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange} modal={false}>
                  <SelectTrigger id="company-currency" className="w-full" data-testid="select-company-currency">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent alignItemWithTrigger={false}>
                    {CURRENCIES.map((c) => (
                      <SelectItem key={c} value={c}>{c}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {form.formState.errors.currency && (
              <p className="text-xs text-destructive">{form.formState.errors.currency.message}</p>
            )}
          </div>

          <Button
            type="submit"
            className="w-full"
            disabled={createMutation.isPending}
            data-testid="btn-create-company"
          >
            {createMutation.isPending ? 'Creando…' : 'Crear empresa'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Card de empresa ────────────────────────────────────────────────────────────

function CompanyCard({ company }: { company: CompanyListItem }) {
  const router = useRouter()
  const sectorLabel = company.sector ? SECTOR_LABEL[company.sector as (typeof SECTORS)[number]] ?? company.sector : null

  return (
    <button
      type="button"
      data-testid="company-card"
      onClick={() => router.push(`/market/${company.idCompany}`)}
      className="flex w-full items-center justify-between gap-4 rounded-lg border border-border bg-card px-5 py-4 text-left transition-colors hover:bg-muted/50"
    >
      <div className="flex min-w-0 items-center gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10">
          <Building2 className="h-4 w-4 text-primary" />
        </span>
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-mono text-sm font-semibold">{company.ticker}</span>
            <span className="truncate text-sm text-muted-foreground">{company.name}</span>
          </div>
          {sectorLabel && <p className="text-xs text-muted-foreground">{sectorLabel}</p>}
        </div>
      </div>

      <div className="flex items-center gap-4 text-right">
        <div className="hidden sm:block">
          <p className="text-xs text-muted-foreground">Último precio</p>
          <p className="text-sm font-semibold tabular-nums">
            {company.lastPrice != null ? formatAmount(company.lastPrice, company.currency) : '—'}
          </p>
        </div>
        <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
      </div>
    </button>
  )
}

// ── Página principal ──────────────────────────────────────────────────────────

export default function MarketPage() {
  const [modalOpen, setModalOpen] = useState(false)
  const [page, setPage] = useState(1)
  const { data, isLoading, isError } = useCompaniesList({ page, pageSize: DEFAULT_PAGE_SIZE })
  const companies = data?.items

  return (
    <div className="mx-auto max-w-6xl px-5 py-8 md:px-8">

      {/* Cabecera */}
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="font-heading text-2xl font-semibold">Mercado</h1>
        <Button onClick={() => setModalOpen(true)} data-testid="btn-nueva-empresa" aria-label="Nueva empresa">
          <Plus className="h-4 w-4 sm:mr-2" />
          <span className="hidden sm:inline">Nueva empresa</span>
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
          Error al cargar las empresas. Inténtalo de nuevo.
        </div>
      )}

      {/* Vacío */}
      {!isLoading && !isError && companies?.length === 0 && (
        <div
          className="rounded-lg border border-border bg-card py-12 text-center"
          data-testid="empty-state"
        >
          <Building2 className="mx-auto mb-3 h-8 w-8 text-muted-foreground/40" />
          <p className="text-sm text-muted-foreground">No hay empresas en el catálogo aún.</p>
          <Button
            variant="outline"
            size="sm"
            className="mt-4"
            onClick={() => setModalOpen(true)}
          >
            Crear la primera empresa
          </Button>
        </div>
      )}

      {/* Lista de cards */}
      {!isLoading && !isError && companies && companies.length > 0 && (
        <>
          <div className="flex flex-col gap-3">
            {companies.map((c) => (
              <CompanyCard key={c.idCompany} company={c} />
            ))}
          </div>
          {data && (
            <Pagination
              page={page}
              pageSize={DEFAULT_PAGE_SIZE}
              totalCount={data.meta.totalCount}
              totalPages={data.meta.totalPages}
              onPageChange={setPage}
            />
          )}
        </>
      )}

      {/* Modal de creación — montado condicionalmente */}
      {modalOpen && (
        <CreateCompanyModal
          open={modalOpen}
          onClose={() => setModalOpen(false)}
        />
      )}

    </div>
  )
}
