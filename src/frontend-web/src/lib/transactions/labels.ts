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
  return new Intl.NumberFormat('es-ES', {
    style:    'currency',
    currency: currency,
    minimumFractionDigits: 2,
  }).format(amount)
}

export function formatDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number)
  return new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: 'short', year: 'numeric' })
    .format(new Date(y, m - 1, d))
}
