import { api } from '@/lib/api'
import type {
  CompanyListItem, Company, CreateCompanyDto, PagedCompanies,
  ValuationListItem, PagedValuations, Valuation, CreateValuationDto, ValuationSeries,
} from '@/types/companies'
import type { PageMeta } from '@/types/pagination'
import type { ValuationPeriod } from '@/types/enums'

export const companyService = {

  // GET /companies?page&pageSize → CompanyListItemDto[] (paginado)
  async listPaged({ page, pageSize }: { page: number; pageSize: number }): Promise<PagedCompanies> {
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) }).toString()
    const result = await api.getWithMeta<CompanyListItem[]>(`/companies?${qs}`)
    const m = result.meta as PageMeta
    return {
      items: result.data ?? [],
      meta: {
        page:       m?.page       ?? 1,
        pageSize:   m?.pageSize   ?? pageSize,
        totalCount: m?.totalCount ?? 0,
        totalPages: m?.totalPages ?? 1,
      },
    }
  },

  // GET /companies/{id} → misma CompanyListItemDto que el listado
  async getById(id: number): Promise<CompanyListItem> {
    return api.get<CompanyListItem>(`/companies/${id}`)
  },

  // POST /companies → CompanyDto
  async create(data: CreateCompanyDto): Promise<Company> {
    return api.post<Company>('/companies', data)
  },

  // GET /companies/{id}/valuations?page&pageSize → ValuationListItemDto[] (paginado)
  async listValuations(id: number, page: number, pageSize: number): Promise<PagedValuations> {
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) }).toString()
    const result = await api.getWithMeta<ValuationListItem[]>(`/companies/${id}/valuations?${qs}`)
    const m = result.meta as PageMeta
    return {
      items: result.data ?? [],
      meta: {
        page:       m?.page       ?? 1,
        pageSize:   m?.pageSize   ?? pageSize,
        totalCount: m?.totalCount ?? 0,
        totalPages: m?.totalPages ?? 1,
      },
    }
  },

  // POST /companies/{id}/valuations → ValuationDto
  async createValuation(id: number, data: CreateValuationDto): Promise<Valuation> {
    return api.post<Valuation>(`/companies/${id}/valuations`, data)
  },

  // GET /companies/{id}/valuations/series?period= → ValuationSeriesDto
  // period viaja como el NOMBRE del miembro del enum (ThreeMonths, OneYear…), no como '3m'/'1y'.
  async valuationSeries(id: number, period: ValuationPeriod): Promise<ValuationSeries> {
    return api.get<ValuationSeries>(`/companies/${id}/valuations/series?period=${period}`)
  },
}
