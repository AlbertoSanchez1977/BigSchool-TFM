import { describe, it, expect } from 'vitest'
import { formatAmount, formatDate } from '@/lib/transactions/labels'

describe('formatAmount', () => {

  it('formatea un importe con moneda válida', () => {
    const result = formatAmount(2850, 'EUR')
    // No acotar al formato exacto: Intl en jsdom/Node puede omitir separadores de miles.
    // Lo relevante es que el número y el símbolo estén presentes.
    expect(result).toContain('2850') // dígitos presentes (con o sin separador de miles)
    expect(result).toContain('€')
  })

  it('usa EUR como fallback cuando currency es string vacío', () => {
    // ?? no protege contra '' (solo null/undefined); este test lo captura
    const result = formatAmount(100, '')
    expect(result).toContain('€')
  })

  it('usa EUR como fallback cuando currency es null/undefined', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = formatAmount(100, null as any)
    expect(result).toContain('€')
  })

  it('formatea correctamente USD', () => {
    const result = formatAmount(500, 'USD')
    expect(result).toContain('500')
  })

  it('formatea 0 correctamente', () => {
    const result = formatAmount(0, 'EUR')
    expect(result).toContain('0')
    expect(result).toContain('€')
  })

})

describe('formatDate', () => {

  it('formatea una fecha ISO a formato legible', () => {
    const result = formatDate('2026-06-30')
    expect(result).toContain('2026')
    expect(result).toContain('30')
  })

  it('no lanza con el primer día del mes', () => {
    expect(() => formatDate('2026-01-01')).not.toThrow()
  })

})
