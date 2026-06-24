import { createApiClient, type ApiClient } from './apiClient'
import { tokenStore } from './auth/tokenStore'

type UnauthorizedHandler = () => void

// Manejador mutable de 401. Se registra desde AuthProvider al montar.
// Este patrón evita la dependencia circular: apiClient no sabe nada de AuthContext.
// Análogo a registrar un callback de error centralizado en un HttpClient de C#.
let unauthorizedHandler: UnauthorizedHandler = () => {}

export function registerUnauthorizedHandler(handler: UnauthorizedHandler): void {
  unauthorizedHandler = handler
}

// Singleton de apiClient listo para usar en cualquier hook/service:
//   import { api } from '@/lib/api'
//   const data = await api.get<Transaction[]>('/transactions')
//
// El token se lee en tiempo de petición (no de importación) → siempre actualizado.
// La URL base viene de la variable de entorno (definida en .env.local).
export const api: ApiClient = createApiClient({
  baseUrl: process.env.NEXT_PUBLIC_API_URL ?? '',
  getToken: () => tokenStore.load()?.accessToken ?? null,
  onUnauthorized: () => unauthorizedHandler(),
})
