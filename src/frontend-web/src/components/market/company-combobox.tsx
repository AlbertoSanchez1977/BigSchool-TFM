'use client'

import { useMemo } from 'react'
import { Combobox } from '@/components/ui/combobox'
import { useCompanies } from '@/hooks/useCompanies'
import { formatAmount } from '@/lib/transactions/labels'

// Envuelve el combobox base: fuente = useCompanies() (?pageSize=100, ver holdingsService),
// 10 resultados iniciales y filtro en cliente por ticker/nombre. Emite idCompany.
// Deuda documentada (spec 011 §6): sin ?search server-side — si el catálogo supera
// los 100 que trae useCompanies, el filtro de aquí queda incompleto.

interface CompanyComboboxProps {
  value: number | null
  onChange: (idCompany: number) => void
  placeholder?: string
}

export function CompanyCombobox({ value, onChange, placeholder = 'Selecciona empresa…' }: CompanyComboboxProps) {
  const { data: companies = [] } = useCompanies()

  const items = useMemo(
    () => companies.map((c) => ({
      value: String(c.idCompany),
      label: c.lastPrice != null
        ? `${c.ticker} — ${c.name} · ${formatAmount(c.lastPrice, c.currency)}`
        : `${c.ticker} — ${c.name}`,
    })),
    [companies],
  )

  return (
    <Combobox
      items={items}
      value={value != null ? String(value) : null}
      onChange={(v) => onChange(Number(v))}
      placeholder={placeholder}
      emptyText="Sin empresas que coincidan."
      initialResultsLimit={10}
      data-testid="select-company"
    />
  )
}
