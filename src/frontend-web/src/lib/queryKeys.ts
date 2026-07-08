// Query key factories — una sola fuente de verdad por recurso, para que los hooks que
// necesitan la misma key (para leer o para invalidar) no la declaren cada uno por su
// lado y acaben divergiendo. Patrón recomendado por TanStack Query:
// https://tkdodo.eu/blog/effective-react-query-keys
//
// DEUDA TÉCNICA (frontend): de momento solo portfolios y companies pasan por aquí.
// El resto de hooks (useProfile → ME_KEY, useCategoryChart/useMonthlySeries → keys
// inline, useTransactions → key inline con filtros) siguen declarando su key en el
// propio fichero. Migrarlos aquí la próxima vez que se toquen, no de una sentada.

export const portfolioKeys = {
  all:     ['portfolios'] as const,
  list:    (page: number, pageSize: number) => ['portfolios', page, pageSize] as const,
  detail:  (id: number) => ['portfolios', id] as const,
  summary: ['portfolios', 'summary'] as const,
}

export const companyKeys = {
  all:    ['companies'] as const,
  list:   (page: number, pageSize: number) => ['companies', 'paged', page, pageSize] as const,
  detail: (id: number) => ['companies', id] as const,
}
