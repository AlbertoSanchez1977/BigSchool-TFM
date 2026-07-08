'use client'

import { useState } from 'react'
import { Check, ChevronsUpDown } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import {
  Command, CommandInput, CommandList, CommandEmpty, CommandGroup, CommandItem,
} from '@/components/ui/command'
import { cn } from '@/lib/utils'

// Combobox genérico (shadcn Command + Popover): un <Select> pero con typeahead —
// filtra la lista mientras escribes, en vez de solo desplegar opciones fijas.
// El filtrado de cmdk es por el `value` que le pasamos a cada CommandItem: por
// eso usamos `item.label` ahí (para que el texto que escribes matchee contra el
// texto visible) y resolvemos el valor real (item.value) por closure en onSelect,
// no por el argumento que cmdk devuelve.

export interface ComboboxItem {
  value: string
  label: string
}

interface ComboboxProps {
  items: ComboboxItem[]
  value: string | null
  onChange: (value: string) => void
  placeholder?: string
  emptyText?: string
  className?: string
  /** Si se indica, solo se muestran los N primeros items mientras no hay texto
   * escrito (evita volcar catálogos grandes de golpe). En cuanto el usuario
   * escribe algo, se filtra sobre la lista completa, no sobre este recorte. */
  initialResultsLimit?: number
  'data-testid'?: string
}

export function Combobox({
  items, value, onChange, placeholder = 'Selecciona…', emptyText = 'Sin resultados.', className,
  initialResultsLimit, 'data-testid': testId,
}: ComboboxProps) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const selected = items.find((i) => i.value === value)

  const visibleItems = !search && initialResultsLimit
    ? items.slice(0, initialResultsLimit)
    : items

  return (
    <Popover open={open} onOpenChange={(o) => { setOpen(o); if (!o) setSearch('') }}>
      <PopoverTrigger
        render={
          <Button
            type="button"
            variant="outline"
            role="combobox"
            aria-expanded={open}
            data-testid={testId}
            className={cn('w-full justify-between font-normal', !selected && 'text-muted-foreground', className)}
          />
        }
      >
        <span className="truncate">{selected ? selected.label : placeholder}</span>
        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
      </PopoverTrigger>
      <PopoverContent className="w-(--anchor-width) p-0" align="start">
        <Command>
          <CommandInput placeholder={placeholder} value={search} onValueChange={setSearch} />
          <CommandList>
            <CommandEmpty>{emptyText}</CommandEmpty>
            <CommandGroup>
              {visibleItems.map((item) => (
                <CommandItem
                  key={item.value}
                  value={item.label}
                  onSelect={() => { onChange(item.value); setOpen(false) }}
                >
                  <Check className={cn('h-4 w-4', item.value === value ? 'opacity-100' : 'opacity-0')} />
                  {item.label}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
