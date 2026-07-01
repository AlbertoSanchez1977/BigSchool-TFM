// Política de refresco de token EN CLIENTE.
//
// Contexto: el backend NO tiene refresh token ni rastreo de tokens en BD. El endpoint
// POST /api/v1/auth/refresh es [Authorize]: recibe el access token AÚN VÁLIDO y devuelve
// uno nuevo. Por tanto el refresco debe ser PROACTIVO (antes de que caduque). Si el token
// caduca del todo, refresh devuelve 401 y toca volver a hacer login.
//
// Como no hay control de servidor, ponemos el límite en cliente: como mucho MAX_REFRESHES
// renovaciones, o una sesión de como mucho MAX_SESSION_HOURS horas. Superado cualquiera de
// los dos, se fuerza re-login. El access token dura ~60 min (JwtSettings.ExpirationMinutes).

export const MAX_REFRESHES = 5
export const MAX_SESSION_HOURS = 24
export const ACCESS_TOKEN_MINUTES = 60 // informativo (default del backend)

// Margen antes de la caducidad a partir del cual conviene refrescar (ms).
const REFRESH_MARGIN_MS = 5 * 60 * 1000 // 5 min

export interface RefreshState {
  refreshCount: number
  sessionStartedAt: number // epoch ms del login inicial
  now: number // epoch ms actual (inyectado para testabilidad)
}

// ¿Se permite aún refrescar, según la política de cliente?
export function canRefresh({ refreshCount, sessionStartedAt, now }: RefreshState): boolean {
  if (refreshCount >= MAX_REFRESHES) return false
  const sessionAgeHours = (now - sessionStartedAt) / (60 * 60 * 1000)
  if (sessionAgeHours >= MAX_SESSION_HOURS) return false
  return true
}

// ¿Está el token lo bastante cerca de caducar (o ya caducado) como para refrescar ya?
export function shouldRefreshSoon(expiresAtIso: string, now: number = Date.now()): boolean {
  const expiresAt = new Date(expiresAtIso).getTime()
  return expiresAt - now <= REFRESH_MARGIN_MS
}
