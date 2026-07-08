'use client'

import { use } from 'react'
import Link from 'next/link'
import { ChevronLeft } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { useCompanyDetail } from '@/hooks/useCompanyDetail'
import { formatAmount } from '@/lib/transactions/labels'
import { SECTOR_LABEL } from '@/lib/investments/labels'
import type { Sector } from '@/types/enums'

export default function CompanyDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params)
  const companyId = Number(id)

  const { data: company, isLoading, isError } = useCompanyDetail(companyId)

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

      {/* ── Serie de cotización + valoraciones (Task 9) ──────────────────── */}
      {!isLoading && !isError && company && (
        <div className="mt-6 rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          Serie de cotización y valoraciones — próximamente.
        </div>
      )}
    </div>
  )
}
