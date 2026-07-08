import type { Sector } from '@/types/enums'

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
