// Espejo de ContactListItemDto / EmailLogListItemDto (módulo Notifications, backend).
// Verificado contra Controllers/Notifications/{ContactsController,EmailsController}.cs
// y Application/Notifications/DTOs/*.
//
// GET /contacts y GET /emails son PAGINADOS (page/pageSize + meta), pero de momento
// se piden con pageSize alto y se ignora la paginación (Task 5 traerá el componente
// reutilizable de paginación; esta es una vista de listado sencilla de administración).

// GET /contacts → ContactListItemDto (Dapper: fecha como string ISO)
export interface Contact {
  idContact: number
  fullName: string
  email: string
  message: string
  createdAt: string // ISO, p.ej. "2026-07-06T10:00:00"
}

// POST /contacts (body) — CreateContactRequest del controller
export interface CreateContactDto {
  fullName: string
  email: string
  message: string
}

// POST /contacts (response) — el controller solo devuelve el id creado, no el Contact completo
export interface CreateContactResult {
  idContact: number
}

// EmailType (Domain, backend): 1 = Welcome, 2 = Contact
export const EMAIL_TYPE = { Welcome: 1, Contact: 2 } as const

// GET /emails → EmailLogListItemDto (Dapper: "type" es short crudo, SIN JsonStringEnumConverter)
// idUser null = email de sistema (acuse de contacto); idUser con valor = email del propio usuario (bienvenida)
export interface EmailLog {
  idEmailLog: number
  idUser: number | null
  recipient: string
  subject: string
  type: number // 1 = Welcome, 2 = Contact (ver EMAIL_TYPE)
  sentAt: string // ISO
}
