import { api } from '@/lib/api'
import type { Contact, CreateContactDto, CreateContactResult, EmailLog } from '@/types/notifications'

// GET /contacts y GET /emails están paginados en el backend; de momento se pide una
// página grande y se ignora meta (ver nota en types/notifications.ts).
const LIST_PAGE_SIZE = 100

export const notificationService = {
  // POST /contacts — público (sin JWT). Responde solo { idContact }.
  createContact: (data: CreateContactDto) =>
    api.post<CreateContactResult>('/contacts', data),

  // GET /contacts — privado (requiere JWT).
  listContacts: () => api.get<Contact[]>(`/contacts?pageSize=${LIST_PAGE_SIZE}`),

  // GET /emails — privado; el backend ya filtra por el usuario autenticado (+ emails de sistema).
  listEmails: () => api.get<EmailLog[]>(`/emails?pageSize=${LIST_PAGE_SIZE}`),
}
