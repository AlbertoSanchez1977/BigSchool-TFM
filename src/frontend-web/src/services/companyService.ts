import { api } from '@/lib/api'
import type {
  CompanyListItem, Company, CreateCompanyDto, PagedCompanies,
} from '@/types/companies'
import type { PageMeta } from '@/types/pagination'

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
}
