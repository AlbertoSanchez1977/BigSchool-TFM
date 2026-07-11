import type { Sector, ValuationPeriod } from '@/types/enums'

// Domain.Investments.Enums.Sector — verificado el listado completo contra el enum real.
export const SECTOR_LABEL: Record<Sector, string> = {
  Technology:    'Tecnología',
  Financials:    'Finanzas',
  Energy:        'Energía',
  Retail:        'Retail',
  Automotive:    'Automoción',
  Healthcare:    'Salud',
  RealEstate:    'Inmobiliario',
  Utilities:     'Utilities',
  ConsumerGoods: 'Bienes de consumo',
  Industrials:   'Industria',
  Other:         'Otro',
}

// Market (códigos de bolsa: NYSE, NASDAQ…) no se traduce — son abreviaturas
// reconocidas internacionalmente, se muestran tal cual.

// Domain.Investments.Enums.ValuationPeriod — etiqueta corta para el selector de periodo
// del gráfico de serie. El valor que viaja a la API es el nombre del miembro (ThreeMonths…),
// esta etiqueta es solo para la UI.
export const VALUATION_PERIOD_LABEL: Record<ValuationPeriod, string> = {
  ThreeMonths: '3M',
  SixMonths:   '6M',
  OneYear:     '1A',
  ThreeYears:  '3A',
  FiveYears:   '5A',
}
