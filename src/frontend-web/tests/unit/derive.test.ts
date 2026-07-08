import { describe, it, expect } from 'vitest'
import {
  deriveMonthlyPoints,
  deriveCumulativeBalance,
  deriveSavingsRate,
} from '@/lib/dashboard/derive'

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makePoint(month: number, income: number, expense: number) {
  return { year: 2026, month, income, expense }
}

// ── deriveMonthlyPoints ───────────────────────────────────────────────────────

describe('deriveMonthlyPoints', () => {

  it('produce 12 puntos con ceros cuando no hay datos', () => {
    const result = deriveMonthlyPoints([])
    expect(result).toHaveLength(12)
    expect(result[0]).toEqual({ month: 1, income: 0, expense: 0 })
    expect(result[11]).toEqual({ month: 12, income: 0, expense: 0 })
  })

  it('usa los valores del backend en los meses con datos', () => {
    const result = deriveMonthlyPoints([makePoint(3, 1000, 500)])
    expect(result[2]).toEqual({ month: 3, income: 1000, expense: 500 })
  })

  it('los meses sin datos mantienen 0 aunque haya otros meses con datos', () => {
    const result = deriveMonthlyPoints([makePoint(6, 2000, 800)])
    expect(result[0]).toEqual({ month: 1, income: 0, expense: 0 })
    expect(result[5]).toEqual({ month: 6, income: 2000, expense: 800 })
    expect(result[11]).toEqual({ month: 12, income: 0, expense: 0 })
  })

  it('mantiene el orden 1..12 independientemente del orden de entrada', () => {
    const result = deriveMonthlyPoints([makePoint(11, 100, 50), makePoint(2, 200, 80)])
    expect(result.map(p => p.month)).toEqual([1,2,3,4,5,6,7,8,9,10,11,12])
    expect(result[1]).toEqual({ month: 2, income: 200, expense: 80 })
    expect(result[10]).toEqual({ month: 11, income: 100, expense: 50 })
  })
})

// ── deriveCumulativeBalance ───────────────────────────────────────────────────

describe('deriveCumulativeBalance', () => {

  it('devuelve 0 para todos los meses cuando no hay transacciones', () => {
    const result = deriveCumulativeBalance([])
    expect(result).toHaveLength(12)
    expect(result.every(p => p.balance === 0)).toBe(true)
  })

  it('acumula income - expense mes a mes', () => {
    const result = deriveCumulativeBalance([
      makePoint(1, 1000, 600),  // mes 1: +400
      makePoint(2, 800, 700),   // mes 2: +100
    ])
    expect(result[0].balance).toBe(400)
    expect(result[1].balance).toBe(500)
  })

  it('los meses sin datos mantienen el acumulado anterior', () => {
    const result = deriveCumulativeBalance([
      makePoint(1, 1000, 600),  // mes 1: +400
      makePoint(3, 500, 200),   // mes 3: +300
    ])
    expect(result[0].balance).toBe(400)
    expect(result[1].balance).toBe(400) // mes 2 sin datos → igual que mes 1
    expect(result[2].balance).toBe(700)
  })

  it('maneja gastos superiores a ingresos produciendo balance negativo', () => {
    const result = deriveCumulativeBalance([
      makePoint(1, 500, 800),   // mes 1: -300
    ])
    expect(result[0].balance).toBe(-300)
  })

  it('devuelve siempre 12 puntos con sus meses 1..12', () => {
    const result = deriveCumulativeBalance([makePoint(5, 1000, 400)])
    expect(result.map(p => p.month)).toEqual([1,2,3,4,5,6,7,8,9,10,11,12])
  })
})

// ── deriveSavingsRate ─────────────────────────────────────────────────────────

describe('deriveSavingsRate', () => {

  it('calcula la tasa de ahorro correctamente: (income - expense) / income * 100', () => {
    expect(deriveSavingsRate(1000, 600)).toBeCloseTo(40)
  })

  it('devuelve null cuando income es 0 (no hay base)', () => {
    expect(deriveSavingsRate(0, 0)).toBeNull()
  })

  it('devuelve null cuando income es negativo (caso anómalo)', () => {
    expect(deriveSavingsRate(-100, 50)).toBeNull()
  })

  it('devuelve valor negativo cuando el gasto supera el ingreso', () => {
    expect(deriveSavingsRate(500, 800)).toBeCloseTo(-60)
  })

  it('devuelve 100 cuando el gasto es 0', () => {
    expect(deriveSavingsRate(1000, 0)).toBeCloseTo(100)
  })
})
