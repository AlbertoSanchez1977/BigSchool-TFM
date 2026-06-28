import { describe, it, expect, beforeEach } from 'vitest'
import { renderHook } from '@testing-library/react'
import { useEmails } from '@/hooks/useEmails'

// TODO (deuda técnica backend): GET /emails
// WHERE idUser = @currentUserId OR idUser IS NULL
// → EmailLogDto[] donde idUser IS NOT NULL = email del usuario (bienvenida)
//                       idUser IS NULL     = emails genéricos (contacto)

const MOCK_USER = { email: 'alberto@bigschool.com', fullName: 'Alberto Sánchez' }

describe('useEmails', () => {
  beforeEach(() => localStorage.clear())

  it('devuelve lista vacía cuando no hay usuario', () => {
    const { result } = renderHook(() => useEmails(null))
    expect(result.current.emails).toHaveLength(0)
  })

  it('devuelve isLoading=false (datos locales, sin fetch async)', () => {
    const { result } = renderHook(() => useEmails(null))
    expect(result.current.isLoading).toBe(false)
  })

  it('incluye el email de bienvenida (idUser asociado) cuando hay usuario', () => {
    const { result } = renderHook(() => useEmails(MOCK_USER))
    const welcome = result.current.emails.find(e => e.type === 'welcome')
    expect(welcome).toBeDefined()
  })

  it('el email de bienvenida va dirigido al email del usuario', () => {
    const { result } = renderHook(() => useEmails(MOCK_USER))
    const welcome = result.current.emails.find(e => e.type === 'welcome')
    expect(welcome?.to).toBe(MOCK_USER.email)
  })

  it('incluye emails de contacto (idUser NULL) de localStorage', () => {
    const stored = [
      {
        id: '1', fullName: 'Alberto', email: 'alberto@bigschool.com',
        message: 'Consulta sobre las inversiones.', submittedAt: '2026-06-28T10:00:00.000Z',
      },
    ]
    localStorage.setItem('contact_submissions', JSON.stringify(stored))

    const { result } = renderHook(() => useEmails(MOCK_USER))
    const contacts = result.current.emails.filter(e => e.type === 'contact')
    expect(contacts).toHaveLength(1)
  })

  it('el total de emails = 1 bienvenida + N contactos localStorage', () => {
    const stored = [
      { id: '1', fullName: 'A', email: 'a@b.com', message: 'Primera consulta enviada', submittedAt: '2026-06-28T10:00:00.000Z' },
      { id: '2', fullName: 'B', email: 'b@c.com', message: 'Segunda consulta enviada', submittedAt: '2026-06-29T10:00:00.000Z' },
    ]
    localStorage.setItem('contact_submissions', JSON.stringify(stored))

    const { result } = renderHook(() => useEmails(MOCK_USER))
    expect(result.current.emails).toHaveLength(3) // 1 welcome + 2 contact
  })

  it('cada EmailLog tiene id, type, to, subject, preview, sentAt', () => {
    const { result } = renderHook(() => useEmails(MOCK_USER))
    const email = result.current.emails[0]
    expect(email).toHaveProperty('id')
    expect(email).toHaveProperty('type')
    expect(email).toHaveProperty('to')
    expect(email).toHaveProperty('subject')
    expect(email).toHaveProperty('preview')
    expect(email).toHaveProperty('sentAt')
  })
})
