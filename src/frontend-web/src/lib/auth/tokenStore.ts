// Claves en localStorage — prefijo 'bs_' (BigSchool) para evitar colisiones con otras apps
const KEYS = {
  ACCESS_TOKEN: 'bs_access_token',
  EXPIRES_AT: 'bs_expires_at',
  SESSION_STARTED_AT: 'bs_session_started_at',
  REFRESH_COUNT: 'bs_refresh_count',
  EMAIL: 'bs_email',
  FULL_NAME: 'bs_full_name',
} as const

// Equivalente a un DTO de sesión en memoria:
//   accessToken  → Bearer que se inyecta en cada petición
//   expiresAt    → ISO 8601 (DateTime) del backend
//   sessionStartedAt → epoch ms del login inicial (para el límite de 24 h)
//   refreshCount → cuántas veces se renovó (para el límite de 5 refrescos)
//   email/fullName → datos del usuario sin necesidad de llamada extra al backend
export interface TokenData {
  accessToken: string
  expiresAt: string
  sessionStartedAt: number
  refreshCount: number
  email: string
  fullName: string
}

export const tokenStore = {
  save(data: TokenData): void {
    localStorage.setItem(KEYS.ACCESS_TOKEN, data.accessToken)
    localStorage.setItem(KEYS.EXPIRES_AT, data.expiresAt)
    localStorage.setItem(KEYS.SESSION_STARTED_AT, String(data.sessionStartedAt))
    localStorage.setItem(KEYS.REFRESH_COUNT, String(data.refreshCount))
    localStorage.setItem(KEYS.EMAIL, data.email)
    localStorage.setItem(KEYS.FULL_NAME, data.fullName)
  },

  load(): TokenData | null {
    const accessToken = localStorage.getItem(KEYS.ACCESS_TOKEN)
    const expiresAt = localStorage.getItem(KEYS.EXPIRES_AT)
    const sessionStartedAt = localStorage.getItem(KEYS.SESSION_STARTED_AT)
    const refreshCount = localStorage.getItem(KEYS.REFRESH_COUNT)
    const email = localStorage.getItem(KEYS.EMAIL)
    const fullName = localStorage.getItem(KEYS.FULL_NAME)

    if (!accessToken || !expiresAt || !sessionStartedAt || !refreshCount || !email || !fullName) {
      return null
    }

    return {
      accessToken,
      expiresAt,
      sessionStartedAt: Number(sessionStartedAt),
      refreshCount: Number(refreshCount),
      email,
      fullName,
    }
  },

  clear(): void {
    Object.values(KEYS).forEach((key) => localStorage.removeItem(key))
  },

  incrementRefreshCount(): void {
    const data = tokenStore.load()
    if (!data) return
    tokenStore.save({ ...data, refreshCount: data.refreshCount + 1 })
  },
}
