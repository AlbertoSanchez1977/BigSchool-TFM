import { describe, it, expect } from 'vitest'
import { contactSchema } from '@/lib/schemas/contact'

const VALID = {
  fullName: 'Alberto Sánchez',
  email: 'alberto@bigschool.com',
  message: 'Hola, tengo una pregunta sobre la plataforma.',
}

describe('contactSchema', () => {
  it('acepta datos correctos', () => {
    expect(contactSchema.safeParse(VALID).success).toBe(true)
  })

  it('rechaza fullName vacío', () => {
    const r = contactSchema.safeParse({ ...VALID, fullName: '' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('fullName')
  })

  it('rechaza fullName con menos de 2 caracteres', () => {
    const r = contactSchema.safeParse({ ...VALID, fullName: 'A' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('fullName')
  })

  it('rechaza email con formato inválido', () => {
    const r = contactSchema.safeParse({ ...VALID, email: 'no-es-un-email' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('email')
  })

  it('rechaza email vacío', () => {
    const r = contactSchema.safeParse({ ...VALID, email: '' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('email')
  })

  it('rechaza mensaje vacío', () => {
    const r = contactSchema.safeParse({ ...VALID, message: '' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('message')
  })

  it('rechaza mensaje con menos de 10 caracteres', () => {
    const r = contactSchema.safeParse({ ...VALID, message: 'Hola' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('message')
  })

  it('rechaza mensaje con más de 1000 caracteres', () => {
    const r = contactSchema.safeParse({ ...VALID, message: 'A'.repeat(1001) })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('message')
  })

  it('acepta mensaje con exactamente 1000 caracteres', () => {
    const r = contactSchema.safeParse({ ...VALID, message: 'A'.repeat(1000) })
    expect(r.success).toBe(true)
  })
})
