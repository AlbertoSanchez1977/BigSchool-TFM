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

// Sector (BigSchool.Domain.Investments.Enums.Sector) — verificado el listado completo
// contra el enum real. Array + tipo derivado, mismo patrón que CURRENCIES (para poder
// iterarlo en <Select>/z.enum()). Traducción al español en lib/investments/labels.ts
// (SECTOR_LABEL) — aquí solo viven los valores en bruto que viajan por la API.
export const SECTORS = [
  'Technology', 'Financials', 'Energy', 'Retail', 'Automotive',
  'Healthcare', 'RealEstate', 'Utilities', 'ConsumerGoods', 'Industrials', 'Other',
] as const
export type Sector = (typeof SECTORS)[number]

// Market (BigSchool.Domain.Investments.Enums.Market) — códigos de bolsa, no se traducen.
export const MARKETS = [
  'NYSE', 'NASDAQ', 'TSX', 'CSE', 'TSXV',
  'LSE', 'AquisExchange', 'CboeUK',
  'Frankfurt', 'Xetra', 'BorseStuttgart', 'BorseMunchen',
  'EuronextParis', 'BME',
] as const
export type Market = (typeof MARKETS)[number]

// ValuationPeriod (BigSchool.Domain.Investments.Enums.ValuationPeriod) — el valor subyacente
// en el backend es el nº de meses de la ventana, pero el query param `?period=` del binding de
// ASP.NET Core acepta el NOMBRE del miembro (o el número), nunca abreviaturas tipo '3m'/'6m'.
// Esas abreviaturas solo existen como etiqueta de UI (ver VALUATION_PERIOD_LABEL en
// lib/investments/labels.ts).
export const VALUATION_PERIODS = ['ThreeMonths', 'SixMonths', 'OneYear', 'ThreeYears', 'FiveYears'] as const
export type ValuationPeriod = (typeof VALUATION_PERIODS)[number]
