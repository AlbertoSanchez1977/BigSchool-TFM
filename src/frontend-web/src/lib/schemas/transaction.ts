import { z } from 'zod'

// Arrays de literales para reutilizarlos tanto en el schema como en el tipo.
// Equivale a enum C# pero como constante de TypeScript.
const TRANSACTION_TYPES     = ['Income', 'Expense']     as const
const MAIN_CATEGORIES       = [
  'EssentialExpenses', 'Investment', 'Savings', 'Donations',
  'Luxuries', 'Education', 'Amortizations',
  'Salary', 'Rentals', 'Dividends', 'Other',
] as const
const CURRENCIES            = ['EUR', 'USD', 'GBP', 'CHF', 'JPY'] as const

export const transactionSchema = z.object({
  type:            z.enum(TRANSACTION_TYPES),
  idMainCategory:  z.enum(MAIN_CATEGORIES),
  idSubCategory:   z.number().int().positive().nullable().optional(),
  description:     z.string().max(500, 'Máximo 500 caracteres').nullable().optional(),
  transactionDate: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Formato de fecha inválido (YYYY-MM-DD)'),
  // register('amount', { valueAsNumber: true }) convierte el string del input a number
  // antes de llegar al schema, por lo que z.number() es suficiente (sin coerce).
  amount: z
    .number({ error: 'El importe debe ser un número' })
    .positive('El importe debe ser mayor que cero'),
  currency: z.enum(CURRENCIES),
})

export type TransactionFormValues = z.infer<typeof transactionSchema>
