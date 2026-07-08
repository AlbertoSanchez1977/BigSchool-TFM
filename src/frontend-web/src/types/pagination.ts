// Forma común de `meta` en todos los endpoints paginados del backend
// (envelope { data, errors[], meta }, meta = { page, pageSize, totalCount, totalPages }).
export interface PageMeta {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

// Tamaños de página estándar — una sola fuente para no repetir el número mágico
// en cada página/servicio (antes: 20 suelto en investments/expenses/market, 100
// suelto en holdingsService/notificationService/dashboard).
// DEFAULT_PAGE_SIZE: listados reales con <Pagination> visible en la UI.
// MAX_PAGE_SIZE: vistas que piden "todo lo razonable" sin paginar (combobox de
// empresa, mini-resumen del Dashboard, listados de administración de Contactos/Emails).
export const DEFAULT_PAGE_SIZE = 20
export const MAX_PAGE_SIZE = 100
