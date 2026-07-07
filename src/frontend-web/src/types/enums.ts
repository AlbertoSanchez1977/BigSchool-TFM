// Espejo de los enums de BigSchool.Domain.Enums.
// El backend usa JsonStringEnumConverter → los enums viajan como el NOMBRE del miembro
// (string), no como número. Por eso aquí son uniones de literales string.

// TransactionType (BigSchool.Domain.Enums.TransactionType)
export type TransactionType = 'Income' | 'Expense'

// MainCategory (BigSchool.Domain.Enums.MainCategory).
// 1-7 son de gasto, 10-13 de ingreso (el valor numérico es informativo; viaja el nombre).
export type MainCategory =
  | 'EssentialExpenses'
  | 'Investment'
  | 'Savings'
  | 'Donations'
  | 'Luxuries'
  | 'Education'
  | 'Amortizations'
  | 'Salary'
  | 'Rentals'
  | 'Dividends'
  | 'Other'

// Currency (BigSchool.Domain.SharedKernel.Enums.Currency). El nombre = código ISO 4217 alpha-3.
// Un union type de TS no existe en runtime (a diferencia de un enum de C#, que sí puedes
// recorrer con Enum.GetValues<T>()). Por eso declaramos primero el array con `as const`
// (valores reales, iterables) y derivamos el tipo de él — así solo hay una fuente de verdad
// para "cuáles son las monedas soportadas", usable tanto en tipos como en <Select> o z.enum().
export const CURRENCIES = ['EUR', 'USD', 'GBP', 'CHF', 'JPY'] as const
export type Currency = (typeof CURRENCIES)[number]
