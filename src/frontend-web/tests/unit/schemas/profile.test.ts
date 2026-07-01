import { describe, it, expect } from 'vitest'
import { profileSchema } from '@/lib/schemas/profile'

const VALID_FULL = {
  fullName: 'Alberto Sánchez',
  password: 'NuevaPass123!',
  confirmPassword: 'NuevaPass123!',
}

describe('profileSchema', () => {
  it('acepta fullName + contraseña válida y coincidente', () => {
    expect(profileSchema.safeParse(VALID_FULL).success).toBe(true)
  })

  it('acepta fullName con password vacío (no cambia contraseña)', () => {
    const r = profileSchema.safeParse({ fullName: 'Alberto Sánchez', password: '', confirmPassword: '' })
    expect(r.success).toBe(true)
  })

  it('rechaza fullName vacío', () => {
    const r = profileSchema.safeParse({ ...VALID_FULL, fullName: '' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('fullName')
  })

  it('rechaza fullName con menos de 2 caracteres', () => {
    const r = profileSchema.safeParse({ ...VALID_FULL, fullName: 'A' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('fullName')
  })

  it('rechaza contraseña con menos de 8 caracteres cuando se proporciona', () => {
    const r = profileSchema.safeParse({ ...VALID_FULL, password: 'corta', confirmPassword: 'corta' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('password')
  })

  it('rechaza cuando las contraseñas no coinciden', () => {
    const r = profileSchema.safeParse({ ...VALID_FULL, confirmPassword: 'OtraDistinta1!' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('confirmPassword')
  })

  it('rechaza cuando se rellena password pero confirmPassword queda vacío', () => {
    const r = profileSchema.safeParse({ ...VALID_FULL, confirmPassword: '' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('confirmPassword')
  })

  it('no exige mínimo de longitud cuando password es vacío (no cambia)', () => {
    const r = profileSchema.safeParse({ fullName: 'Test User', password: '', confirmPassword: '' })
    expect(r.success).toBe(true)
  })
})
