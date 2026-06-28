import { describe, it, expect } from 'vitest'
import { apiKeySchema } from '@/lib/schemas/aiScanner'

describe('apiKeySchema', () => {
  it('acepta provider y key válidos', () => {
    const r = apiKeySchema.safeParse({ provider: 'claude', key: 'sk-ant-1234567890' })
    expect(r.success).toBe(true)
  })

  it('rechaza key vacía', () => {
    const r = apiKeySchema.safeParse({ provider: 'claude', key: '' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('key')
  })

  it('rechaza key demasiado corta (< 10 caracteres)', () => {
    const r = apiKeySchema.safeParse({ provider: 'claude', key: 'short' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('key')
  })

  it('rechaza provider vacío', () => {
    const r = apiKeySchema.safeParse({ provider: '', key: 'sk-ant-1234567890' })
    expect(r.success).toBe(false)
    expect(r.error?.issues[0].path).toContain('provider')
  })

  it('rechaza cuando faltan ambos campos', () => {
    const r = apiKeySchema.safeParse({})
    expect(r.success).toBe(false)
    expect(r.error?.issues.length).toBeGreaterThanOrEqual(2)
  })
})
