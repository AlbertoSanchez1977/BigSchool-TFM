'use client'

import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { useRouter } from 'next/navigation'
import { tokenStore } from './tokenStore'
import { canRefresh, shouldRefreshSoon } from './refreshPolicy'
import { api, registerUnauthorizedHandler } from '../api'
import type { LoginDto, RegisterDto, AuthResponse } from '../../types/auth'

// ── Tipos públicos ────────────────────────────────────────────────────────────

export interface AuthUser {
  email: string
  fullName: string
  // Preparado para cuando AuthResponseDto incluya currency. Será undefined hasta entonces.
  currency?: import('../../types/enums').Currency
}

// Forma análoga a un servicio de sesión en C#:
//   user      → identidad actual (null = no autenticado)
//   token     → Bearer que el apiClient ya inyecta por su cuenta
//   isLoading → true mientras se hidrata desde localStorage (evita flicker en guards)
export interface AuthContextValue {
  user: AuthUser | null
  token: string | null
  isLoading: boolean
  login(dto: LoginDto): Promise<void>
  register(dto: RegisterDto): Promise<void>
  logout(): void
  refresh(): Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

// Exportado para poder importarlo en tests sin hardcodear el número
export const REFRESH_CHECK_INTERVAL_MS = 30_000

// ── AuthProvider ──────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [token, setToken] = useState<string | null>(null)
  const [expiresAt, setExpiresAt] = useState<string | null>(null)
  // isLoading=true hasta que montamos en cliente y leemos localStorage.
  // CLAVE: servidor y cliente renderizan IGUAL en el primer render (user=null, isLoading=true),
  // por eso NO hay error de hidratación. La UI usa isLoading para mostrar un placeholder
  // neutro en vez del estado deslogueado, evitando el parpadeo "Login/Registro → Dashboard".
  const [isLoading, setIsLoading] = useState(true)
  const router = useRouter()

  // Hidratación al montar: restaurar sesión previa desde localStorage.
  // Corre después del primer pintado → no afecta al HTML del servidor → sin mismatch.
  useEffect(() => {
    const data = tokenStore.load()
    if (data) {
      setUser({ email: data.email, fullName: data.fullName })
      setToken(data.accessToken)
      setExpiresAt(data.expiresAt)
    }
    setIsLoading(false)
  }, [])

  const logout = useCallback(() => {
    tokenStore.clear()
    setUser(null)
    setToken(null)
    setExpiresAt(null)
  }, [])

  const login = useCallback(async (dto: LoginDto) => {
    const response = await api.post<AuthResponse>('/auth/login', dto)
    tokenStore.save({
      accessToken: response.accessToken,
      expiresAt: response.expiresAt,
      email: response.email,
      fullName: response.fullName,
      sessionStartedAt: Date.now(), // inicio de sesión fresco
      refreshCount: 0,
    })
    setUser({ email: response.email, fullName: response.fullName, currency: response.currency })
    setToken(response.accessToken)
    setExpiresAt(response.expiresAt)
  }, [])

  const register = useCallback(async (dto: RegisterDto) => {
    const response = await api.post<AuthResponse>('/auth/register', dto)
    tokenStore.save({
      accessToken: response.accessToken,
      expiresAt: response.expiresAt,
      email: response.email,
      fullName: response.fullName,
      sessionStartedAt: Date.now(),
      refreshCount: 0,
    })
    setUser({ email: response.email, fullName: response.fullName, currency: response.currency })
    setToken(response.accessToken)
    setExpiresAt(response.expiresAt)
  }, [])

  const refresh = useCallback(async () => {
    try {
      // POST /api/v1/auth/refresh — sin body; el backend lee el token desde el header Authorization.
      // El apiClient singleton inyecta Bearer desde tokenStore automáticamente.
      const response = await api.post<AuthResponse>('/auth/refresh', {})
      const existing = tokenStore.load()
      tokenStore.save({
        accessToken: response.accessToken,
        expiresAt: response.expiresAt,
        email: response.email,
        fullName: response.fullName,
        sessionStartedAt: existing?.sessionStartedAt ?? Date.now(),
        refreshCount: (existing?.refreshCount ?? 0) + 1,
      })
      setUser({ email: response.email, fullName: response.fullName, currency: response.currency })
      setToken(response.accessToken)
      setExpiresAt(response.expiresAt)
    } catch {
      // El token caducó del todo o el refresh fue rechazado → re-login obligatorio
      logout()
      router.push('/')
    }
  }, [logout, router])

  // Timer proactivo de refresco.
  // Se recrea solo si cambia token/expiresAt (es decir, tras login o refresh).
  // Intervalo de 30 s: comprueba shouldRefreshSoon; si el token está a ≤5 min de caducar,
  // actúa según la política canRefresh (max 5 refrescos / 24 h de sesión).
  useEffect(() => {
    if (!token || !expiresAt) return

    const interval = setInterval(() => {
      if (!shouldRefreshSoon(expiresAt)) return

      const data = tokenStore.load()
      if (!data) return

      if (canRefresh({ refreshCount: data.refreshCount, sessionStartedAt: data.sessionStartedAt, now: Date.now() })) {
        void refresh()
      } else {
        logout()
        router.push('/')
      }
    }, REFRESH_CHECK_INTERVAL_MS)

    return () => clearInterval(interval)
  }, [token, expiresAt, refresh, logout, router])

  // Registrar el handler de 401 en el apiClient singleton.
  // Así cualquier petición protegida que devuelva 401 cerrará la sesión automáticamente.
  useEffect(() => {
    registerUnauthorizedHandler(() => {
      logout()
      router.push('/login')
    })
  }, [logout, router])

  return (
    <AuthContext.Provider value={{ user, token, isLoading, login, register, logout, refresh }}>
      {children}
    </AuthContext.Provider>
  )
}

// ── useAuth ───────────────────────────────────────────────────────────────────

// Análogo a inyectar ICurrentUserService en un controller de C#.
// Lanza si se usa fuera del provider (fallo en diseño, no de runtime normal).
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  return ctx
}
