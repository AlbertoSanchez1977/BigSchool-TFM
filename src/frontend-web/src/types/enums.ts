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

// Currency (BigSchool.Domain.Enums.Currency). El nombre = código ISO 4217 alpha-3.
export type Currency = 'EUR' | 'USD' | 'GBP' | 'CHF' | 'JPY'
