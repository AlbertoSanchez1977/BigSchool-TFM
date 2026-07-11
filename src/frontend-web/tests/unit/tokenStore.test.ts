import { describe, it, expect, beforeEach } from 'vitest'
import { tokenStore, type TokenData } from '@/lib/auth/tokenStore'

// Fixture reutilizable — refleja la forma real del TokenData
const SAMPLE: TokenData = {
  accessToken: 'tok_abc123',
  expiresAt: '2026-06-24T14:00:00Z',
  sessionStartedAt: 1_750_000_000_000,
  refreshCount: 0,
  email: 'user@bigschool.com',
  fullName: 'Test User',
}

describe('tokenStore', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('save + load hace round-trip de todos los campos', () => {
    tokenStore.save(SAMPLE)
    expect(tokenStore.load()).toEqual(SAMPLE)
  })

  it('load devuelve null cuando localStorage está vacío', () => {
    expect(tokenStore.load()).toBeNull()
  })

  it('load devuelve null si falta cualquier campo (datos corruptos)', () => {
    localStorage.setItem('bs_access_token', 'tok')
    // El resto de campos no están → datos incompletos
    expect(tokenStore.load()).toBeNull()
  })

  it('clear elimina todos los campos del localStorage', () => {
    tokenStore.save(SAMPLE)
    tokenStore.clear()
    expect(tokenStore.load()).toBeNull()
    // Verificar que no queda ninguna clave bs_*
    expect(localStorage.getItem('bs_access_token')).toBeNull()
    expect(localStorage.getItem('bs_email')).toBeNull()
  })

  it('incrementRefreshCount aumenta refreshCount en exactamente 1', () => {
    tokenStore.save({ ...SAMPLE, refreshCount: 2 })
    tokenStore.incrementRefreshCount()
    expect(tokenStore.load()?.refreshCount).toBe(3)
  })

  it('incrementRefreshCount no lanza error si no hay sesión guardada', () => {
    expect(() => tokenStore.incrementRefreshCount()).not.toThrow()
    expect(tokenStore.load()).toBeNull()
  })

  it('updateFullName actualiza solo fullName, preservando el resto de campos', () => {
    tokenStore.save(SAMPLE)
    tokenStore.updateFullName('Nuevo Nombre')
    expect(tokenStore.load()).toEqual({ ...SAMPLE, fullName: 'Nuevo Nombre' })
  })

  it('updateFullName no lanza error si no hay sesión guardada', () => {
    expect(() => tokenStore.updateFullName('Nuevo Nombre')).not.toThrow()
    expect(tokenStore.load()).toBeNull()
  })
})
