import type { MainCategory, TransactionType } from '@/types/enums'

export const TRANSACTION_TYPE_LABEL: Record<TransactionType, string> = {
  Income:  'Ingreso',
  Expense: 'Gasto',
}

export const MAIN_CATEGORY_LABEL: Record<MainCategory, string> = {
  EssentialExpenses: 'Gastos esenciales',
  Investment:        'Inversión',
  Savings:           'Ahorro',
  Donations:         'Donaciones',
  Luxuries:          'Ocio y lujo',
  Education:         'Educación',
  Amortizations:     'Amortizaciones',
  Salary:            'Nómina',
  Rentals:           'Alquileres',
  Dividends:         'Dividendos',
  Other:             'Otros',
}

export function formatAmount(amount: number, currency: string): string {
  // || en lugar de ?? para cubrir también string vacío '' (que ?? dejaría pasar).
  // Intl.NumberFormat lanza RangeError si currency es vacío o inválido.
  const safeCurrency = currency || 'EUR'
  return new Intl.NumberFormat('es-ES', {
    style:    'currency',
    currency: safeCurrency,
    minimumFractionDigits: 2,
  }).format(amount)
}

export function formatDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number)
  return new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: 'short', year: 'numeric' })
    .format(new Date(y, m - 1, d))
}

// Formatea un porcentaje con locale es-ES (separador decimal coma).
export function formatPct(value: number, decimals = 1): string {
  return value.toLocaleString('es-ES', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }) + ' %'
}

// Clase CSS para colorear valores de PnL / balance / tasa de ahorro.
export function colorPnL(value: number): string {
  if (value > 0) return 'text-positive'
  if (value < 0) return 'text-negative'
  return ''
}

// Prefijo '+' para valores positivos (negativos llevan el signo automático).
export function signPnL(value: number): string {
  return value > 0 ? '+' : ''
}
