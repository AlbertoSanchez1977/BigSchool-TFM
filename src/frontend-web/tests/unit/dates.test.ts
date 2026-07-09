import { describe, it, expect } from 'vitest'
import { defaultTransactionDate, isNotFuture } from '@/lib/dates'

describe('defaultTransactionDate', () => {
  // "hoy" fijo para determinismo: 2026-07-09
  const today = new Date(2026, 6, 9) // meses 0-indexados: 6 = julio

  it('devuelve la fecha de hoy si el periodo visible es el mes/año actual', () => {
    expect(defaultTransactionDate(2026, 7, today)).toBe('2026-07-09')
  })

  it('devuelve el día 1 si el periodo visible es un mes anterior', () => {
    expect(defaultTransactionDate(2026, 3, today)).toBe('2026-03-01')
  })

  it('devuelve el día 1 si el periodo visible es un mes de otro año', () => {
    expect(defaultTransactionDate(2025, 12, today)).toBe('2025-12-01')
  })

  it('devuelve el día 1 si el periodo visible es un mes futuro', () => {
    expect(defaultTransactionDate(2026, 9, today)).toBe('2026-09-01')
  })
})

describe('isNotFuture', () => {
  const today = new Date(2026, 6, 9)

  it('acepta hoy', () => {
    expect(isNotFuture('2026-07-09', today)).toBe(true)
  })

  it('acepta una fecha pasada', () => {
    expect(isNotFuture('2026-07-08', today)).toBe(true)
  })

  it('rechaza una fecha futura', () => {
    expect(isNotFuture('2026-07-10', today)).toBe(false)
  })
})
