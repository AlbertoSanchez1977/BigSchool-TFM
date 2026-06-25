'use client'

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { useAuth } from '@/hooks/useAuth'
import { NavbarPrivate } from '@/components/navbar-private'

// Guard de ruta privada — análogo al atributo [Authorize] de ASP.NET Core.
// Se ejecuta ANTES de renderizar cualquier página bajo (private)/.
// Mientras isLoading=true no actuamos: el AuthProvider todavía está leyendo
// localStorage; si redirigiéramos ahora mandaríamos al usuario a "/" aunque
// tuviera sesión activa.
export default function PrivateLayout({ children }: { children: React.ReactNode }) {
  const { user, isLoading } = useAuth()
  const router = useRouter()

  useEffect(() => {
    if (!isLoading && !user) {
      router.push('/')
    }
  }, [isLoading, user, router])

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-border border-t-primary" />
      </div>
    )
  }

  if (!user) {
    return null
  }

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <NavbarPrivate />
      <main className="flex-1">{children}</main>
    </div>
  )
}
