// Re-exporta useAuth desde lib/auth para que los componentes usen la ruta hooks/
// en lugar de lib/auth/AuthProvider (separación de capas: hooks/ vs lib/).
export { useAuth } from '@/lib/auth/AuthProvider'
export type { AuthUser, AuthContextValue } from '@/lib/auth/AuthProvider'
