'use client'
import { useMemo } from 'react'
import { loadContacts } from '@/hooks/useContacts'

// ── Contrato de backend (deuda técnica) ───────────────────────────────────────
// TODO (deuda técnica backend): GET /emails
// WHERE idUser = @currentUserId OR idUser IS NULL
// → EmailLogDto[] donde:
//   idUser IS NOT NULL = email asociado al usuario (bienvenida, etc.)
//   idUser IS NULL     = email genérico del sistema (contacto, notificación)
// Mock local: bienvenida derivada del usuario autenticado + contactos de localStorage.

export interface EmailLog {
  id: string
  type: 'welcome' | 'contact'
  to: string
  subject: string
  preview: string
  sentAt: string | null
}

interface UserLike {
  email: string
  fullName: string
}

export function useEmails(user: UserLike | null): { emails: EmailLog[]; isLoading: boolean } {
  const emails = useMemo<EmailLog[]>(() => {
    if (!user) return []

    const result: EmailLog[] = []

    // 1. Email de bienvenida — idUser = usuario actual (simulado)
    result.push({
      id: `welcome-${user.email}`,
      type: 'welcome',
      to: user.email,
      subject: `Bienvenido a BigSchool, ${user.fullName}`,
      preview: 'Gracias por registrarte. Ya puedes empezar a controlar tus finanzas e inversiones.',
      sentAt: null, // fecha de registro no disponible en cliente hasta que el backend la exponga
    })

    // 2. Emails de contacto — idUser IS NULL (mensajes genéricos del sistema)
    const contacts = loadContacts()
    contacts.forEach(c => {
      result.push({
        id: `contact-${c.id}`,
        type: 'contact',
        to: 'soporte@bigschool.com',
        subject: `Nuevo mensaje de contacto — ${c.fullName}`,
        preview: c.message.length > 120 ? c.message.slice(0, 120) + '…' : c.message,
        sentAt: c.submittedAt,
      })
    })

    return result
  }, [user])

  return { emails, isLoading: false }
}
