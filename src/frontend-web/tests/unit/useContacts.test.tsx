import { describe, it, expect, beforeEach } from 'vitest'
import { renderHook } from '@testing-library/react'
import { useContacts } from '@/hooks/useContacts'

// jsdom proporciona localStorage en el entorno de test.
// Limpiamos entre tests para evitar estado compartido.

describe('useContacts', () => {
  beforeEach(() => localStorage.clear())

  it('devuelve lista vacía cuando no hay nada en localStorage', () => {
    const { result } = renderHook(() => useContacts())
    expect(result.current.contacts).toHaveLength(0)
  })

  it('devuelve los contactos almacenados en localStorage', () => {
    const stored = [
      {
        id: 'abc-1',
        fullName: 'Alberto Sánchez',
        email: 'a@b.com',
        message: 'Tengo una pregunta',
        submittedAt: '2026-06-28T10:00:00.000Z',
      },
    ]
    localStorage.setItem('contact_submissions', JSON.stringify(stored))

    const { result } = renderHook(() => useContacts())
    expect(result.current.contacts).toHaveLength(1)
    expect(result.current.contacts[0].fullName).toBe('Alberto Sánchez')
  })

  it('devuelve lista vacía si el JSON almacenado es inválido', () => {
    localStorage.setItem('contact_submissions', 'no-es-json')
    const { result } = renderHook(() => useContacts())
    expect(result.current.contacts).toHaveLength(0)
  })

  it('cada contacto tiene las propiedades esperadas', () => {
    const stored = [
      { id: '1', fullName: 'Test', email: 't@t.com', message: 'Hola mundo', submittedAt: '2026-06-28' },
      { id: '2', fullName: 'Test2', email: 't2@t.com', message: 'Otro mensaje', submittedAt: '2026-06-29' },
    ]
    localStorage.setItem('contact_submissions', JSON.stringify(stored))

    const { result } = renderHook(() => useContacts())
    expect(result.current.contacts).toHaveLength(2)
    const first = result.current.contacts[0]
    expect(first).toHaveProperty('id')
    expect(first).toHaveProperty('fullName')
    expect(first).toHaveProperty('email')
    expect(first).toHaveProperty('message')
    expect(first).toHaveProperty('submittedAt')
  })
})
