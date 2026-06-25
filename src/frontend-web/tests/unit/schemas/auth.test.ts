import { describe, it, expect } from 'vitest'
import { loginSchema, registerSchema } from '@/lib/schemas/auth'

// Estos tests verifican las reglas de validación de los formularios de auth.
// Son los guardianes del contrato entre el formulario y el backend — si el backend
// cambia los requisitos de password o email, empezamos aquí.

describe('loginSchema', () => {
  it('acepta email y password válidos', () => {
    const result = loginSchema.safeParse({ email: 'user@bigschool.com', password: 'secret123' })
    expect(result.success).toBe(true)
  })

  it('rechaza un email con formato incorrecto', () => {
    const result = loginSchema.safeParse({ email: 'no-es-un-email', password: 'secret123' })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].path).toContain('email')
  })

  it('rechaza password vacío', () => {
    const result = loginSchema.safeParse({ email: 'user@bigschool.com', password: '' })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].path).toContain('password')
  })

  it('rechaza campos ausentes', () => {
    const result = loginSchema.safeParse({})
    expect(result.success).toBe(false)
    expect(result.error?.issues.length).toBeGreaterThanOrEqual(2)
  })
})

describe('registerSchema', () => {
  const VALID = {
    fullName: 'Alberto Sánchez',
    email: 'alberto@bigschool.com',
    password: 'Segura123!',
    confirmPassword: 'Segura123!',
  }

  it('acepta datos correctos', () => {
    expect(registerSchema.safeParse(VALID).success).toBe(true)
  })

  it('rechaza email inválido', () => {
    const result = registerSchema.safeParse({ ...VALID, email: 'malo' })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].path).toContain('email')
  })

  it('rechaza fullName con menos de 2 caracteres', () => {
    const result = registerSchema.safeParse({ ...VALID, fullName: 'A' })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].path).toContain('fullName')
  })

  it('rechaza password con menos de 8 caracteres', () => {
    const result = registerSchema.safeParse({ ...VALID, password: 'corta', confirmPassword: 'corta' })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].path).toContain('password')
  })

  it('rechaza cuando confirmPassword no coincide con password', () => {
    const result = registerSchema.safeParse({ ...VALID, confirmPassword: 'OtraPassword1!' })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].path).toContain('confirmPassword')
  })

  it('rechaza campos ausentes', () => {
    const result = registerSchema.safeParse({})
    expect(result.success).toBe(false)
    expect(result.error?.issues.length).toBeGreaterThanOrEqual(3)
  })
})
