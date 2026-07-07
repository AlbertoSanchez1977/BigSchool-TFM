// Forma común de `meta` en todos los endpoints paginados del backend
// (envelope { data, errors[], meta }, meta = { page, pageSize, totalCount, totalPages }).
export interface PageMeta {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
