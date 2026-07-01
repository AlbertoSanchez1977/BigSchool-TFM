import type { Currency } from './enums'

// POST /api/v1/auth/login  → LoginCommand
export interface LoginDto {
  email: string
  password: string
}

// POST /api/v1/auth/register → RegisterCommand
export interface RegisterDto {
  email: string
  password: string
  fullName: string
}

// Respuesta de login/register/refresh → AuthResponseDto.
// OJO: el backend NO emite refresh token ni objeto user. El control de sesión
// (refresco proactivo y política de caducidad) es 100% en cliente — ver lib/auth/refreshPolicy.
export interface AuthResponse {
  accessToken: string
  expiresAt: string  // ISO 8601 (DateTime) — momento de caducidad del token
  email: string
  fullName: string
  // DEUDA TÉCNICA: el backend no devuelve currency aún (siempre crea EUR por defecto).
  // Campo preparado para cuando AuthResponseDto incluya la moneda del usuario.
  // Hasta entonces será undefined y los formularios usarán 'EUR' como fallback.
  currency?: Currency
}
