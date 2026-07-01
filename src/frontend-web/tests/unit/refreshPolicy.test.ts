import { describe, it, expect } from 'vitest'
import {
  canRefresh,
  shouldRefreshSoon,
  MAX_REFRESHES,
  MAX_SESSION_HOURS,
} from '@/lib/auth/refreshPolicy'

const HOUR = 60 * 60 * 1000

describe('refreshPolicy', () => {
  describe('canRefresh', () => {
    it('permite refrescar al inicio de la sesión (0 refrescos, recién logueado)', () => {
      const now = Date.now()
      expect(canRefresh({ refreshCount: 0, sessionStartedAt: now, now })).toBe(true)
    })

    it('bloquea al alcanzar el máximo de refrescos', () => {
      const now = Date.now()
      expect(
        canRefresh({ refreshCount: MAX_REFRESHES, sessionStartedAt: now, now }),
      ).toBe(false)
    })

    it('permite justo por debajo del máximo de refrescos', () => {
      const now = Date.now()
      expect(
        canRefresh({ refreshCount: MAX_REFRESHES - 1, sessionStartedAt: now, now }),
      ).toBe(true)
    })

    it('bloquea cuando la sesión supera el máximo de horas', () => {
      const now = Date.now()
      const sessionStartedAt = now - (MAX_SESSION_HOURS + 1) * HOUR
      expect(canRefresh({ refreshCount: 1, sessionStartedAt, now })).toBe(false)
    })

    it('permite justo por debajo del límite de horas', () => {
      const now = Date.now()
      const sessionStartedAt = now - (MAX_SESSION_HOURS - 1) * HOUR
      expect(canRefresh({ refreshCount: 1, sessionStartedAt, now })).toBe(true)
    })

    it('bloquea si se superan AMBOS límites', () => {
      const now = Date.now()
      const sessionStartedAt = now - (MAX_SESSION_HOURS + 5) * HOUR
      expect(
        canRefresh({ refreshCount: MAX_REFRESHES + 2, sessionStartedAt, now }),
      ).toBe(false)
    })
  })

  describe('shouldRefreshSoon', () => {
    it('devuelve true cuando faltan menos de 5 min para caducar', () => {
      const now = Date.now()
      const expiresAt = new Date(now + 4 * 60 * 1000).toISOString()
      expect(shouldRefreshSoon(expiresAt, now)).toBe(true)
    })

    it('devuelve false cuando aún queda margen', () => {
      const now = Date.now()
      const expiresAt = new Date(now + 30 * 60 * 1000).toISOString()
      expect(shouldRefreshSoon(expiresAt, now)).toBe(false)
    })

    it('devuelve true si el token ya caducó', () => {
      const now = Date.now()
      const expiresAt = new Date(now - 1000).toISOString()
      expect(shouldRefreshSoon(expiresAt, now)).toBe(true)
    })
  })
})
