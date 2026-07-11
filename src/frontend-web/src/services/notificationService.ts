import { api } from '@/lib/api'
import type { Contact, CreateContactDto, CreateContactResult, EmailLog } from '@/types/notifications'
import { MAX_PAGE_SIZE } from '@/types/pagination'

export const notificationService = {
  // POST /contacts — público (sin JWT). Responde solo { idContact }.
  createContact: (data: CreateContactDto) =>
    api.post<CreateContactResult>('/contacts', data),

  // GET /contacts — privado (requiere JWT). GET /contacts y GET /emails están paginados en el
  // backend; de momento se pide una página grande y se ignora meta (ver nota en types/notifications.ts).
  listContacts: () => api.get<Contact[]>(`/contacts?pageSize=${MAX_PAGE_SIZE}`),

  // GET /emails — privado; el backend ya filtra por el usuario autenticado (+ emails de sistema).
  listEmails: () => api.get<EmailLog[]>(`/emails?pageSize=${MAX_PAGE_SIZE}`),
}
