'use client'

import { useRef, useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { LineChart, Menu, X, LayoutDashboard } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { LoginForm } from '@/components/auth/login-form'
import { RegisterForm } from '@/components/auth/register-form'
import { ProfileDropdown } from '@/components/auth/profile-dropdown'
import { useAuth } from '@/hooks/useAuth'
import { cn } from '@/lib/utils'

type AuthMode = 'login' | 'register'

const navLinks = [
  { label: 'Gastos e ingresos', href: '/#gastos' },
  { label: 'Inversiones', href: '/#inversiones' },
  { label: 'AI Scanner', href: '/#ai-scanner' },
  { label: 'Contacto', href: '/#contacto' },
  { label: 'Alcance', href: '/scope' },
]

export function NavbarPublic() {
  const router = useRouter()
  const { user, isLoading } = useAuth()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [panelOpen, setPanelOpen] = useState(false)
  const [authMode, setAuthMode] = useState<AuthMode>('login')

  // Ref sobre el contenedor que engloba botones + panel flotante.
  // El listener de mousedown cierra el panel cuando el click ocurre FUERA de él.
  const authAreaRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!panelOpen) return
    function handleOutside(e: MouseEvent) {
      const target = e.target as HTMLElement
      // Los <Select> del panel (p.ej. moneda base del registro) pintan su desplegable
      // en un Portal, fuera del DOM de authAreaRef (para no quedar recortados por
      // overflow). Sin este chequeo, elegir una opción se interpreta como "click
      // fuera" y cierra el panel entero antes de que el usuario pueda enviar el
      // formulario. data-slot="select-content" es el marcador que pone shadcn/Base UI
      // en el desplegable — target.closest lo encuentra aunque el nodo esté fuera
      // del árbol de authAreaRef, porque closest() sube por el DOM real, no por React.
      if (target.closest('[data-slot="select-content"]')) return
      if (authAreaRef.current && !authAreaRef.current.contains(target)) {
        setPanelOpen(false)
      }
    }
    document.addEventListener('mousedown', handleOutside)
    return () => document.removeEventListener('mousedown', handleOutside)
  }, [panelOpen])

  function openAuth(mode: AuthMode) {
    setAuthMode(mode)
    setPanelOpen(true)
    setMobileOpen(false)
  }

  function closeAuth() {
    setPanelOpen(false)
  }

  return (
    <header className="sticky top-0 z-50 w-full border-b border-border bg-background/80 backdrop-blur-md">
      <nav className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between px-5 md:px-8">

        {/* ── Logo ─────────────────────────────────────────────────────────── */}
        <a href="/" className="flex items-center gap-2" aria-label="BigSchool inicio">
          <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <LineChart className="h-5 w-5" aria-hidden="true" />
          </span>
          <span className="font-heading text-lg font-semibold tracking-tight">BigSchool</span>
        </a>

        {/* ── Links centrales (solo desktop) ───────────────────────────────── */}
        <ul className="hidden items-center gap-7 md:flex">
          {navLinks.map((link) => (
            <li key={link.href}>
              <a
                href={link.href}
                className="text-sm font-medium text-muted-foreground transition-colors hover:text-foreground"
              >
                {link.label}
              </a>
            </li>
          ))}
        </ul>

        {/* ── Zona derecha desktop ─────────────────────────────────────────── */}
        <div ref={authAreaRef} className="relative hidden items-center gap-2 md:flex">
          {isLoading ? (
            // Placeholder neutro mientras leemos localStorage tras montar.
            // Servidor y cliente renderizan igual (isLoading=true) → sin error de hidratación.
            <div className="h-7 w-[120px] animate-pulse rounded-md bg-muted" aria-hidden="true" />
          ) : user ? (
            // Usuario autenticado: acceso al Dashboard + ProfileDropdown (perfil/logout)
            <>
              <Button size="sm" onClick={() => router.push('/dashboard')}>
                <LayoutDashboard className="mr-1.5 h-4 w-4" />
                Ir al Dashboard
              </Button>
              <ProfileDropdown />
            </>
          ) : (
            // Usuario no autenticado: panel flotante de login/registro
            <>
              <Button variant="ghost" size="sm" onClick={() => openAuth('login')}>
                Iniciar sesión
              </Button>
              <Button size="sm" onClick={() => openAuth('register')}>
                Registrarse
              </Button>

              {panelOpen && (
                <div className="absolute right-0 top-[calc(100%+8px)] z-50 w-80 rounded-lg border border-border bg-popover p-6 text-popover-foreground shadow-lg ring-1 ring-foreground/10">
                  {authMode === 'login' ? (
                    <LoginForm
                      onSwitchMode={() => setAuthMode('register')}
                      onSuccess={closeAuth}
                    />
                  ) : (
                    <RegisterForm
                      onSwitchMode={() => setAuthMode('login')}
                      onSuccess={closeAuth}
                    />
                  )}
                </div>
              )}
            </>
          )}
        </div>

        {/* ── Botón hamburguesa (solo móvil) ───────────────────────────────── */}
        <button
          type="button"
          className="inline-flex h-9 w-9 items-center justify-center rounded-md text-foreground md:hidden"
          onClick={() => setMobileOpen((v) => !v)}
          aria-label={mobileOpen ? 'Cerrar menú' : 'Abrir menú'}
          aria-expanded={mobileOpen}
        >
          {mobileOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </nav>

      {/* ── Menú móvil expandido ─────────────────────────────────────────────── */}
      <div className={cn('border-t border-border md:hidden', mobileOpen ? 'block' : 'hidden')}>
        <ul className="mx-auto flex w-full max-w-6xl flex-col gap-1 px-5 py-3">
          {navLinks.map((link) => (
            <li key={link.href}>
              <a
                href={link.href}
                onClick={() => setMobileOpen(false)}
                className="block rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
              >
                {link.label}
              </a>
            </li>
          ))}
          <li className="mt-2 flex flex-col gap-2 px-1">
            {isLoading ? (
              <div className="h-7 w-full animate-pulse rounded-md bg-muted" aria-hidden="true" />
            ) : user ? (
              <>
                <Button
                  size="sm"
                  className="w-full"
                  onClick={() => { setMobileOpen(false); router.push('/dashboard') }}
                >
                  <LayoutDashboard className="mr-1.5 h-4 w-4" />
                  Ir al Dashboard
                </Button>
                <div className="flex justify-start px-1">
                  <ProfileDropdown />
                </div>
              </>
            ) : (
              <>
                {/* Móvil: navega a páginas físicas de auth (el Popover no encaja en pantallas pequeñas) */}
                <Button
                  variant="outline"
                  size="sm"
                  className="w-full"
                  onClick={() => { setMobileOpen(false); router.push('/login') }}
                >
                  Iniciar sesión
                </Button>
                <Button
                  size="sm"
                  className="w-full"
                  onClick={() => { setMobileOpen(false); router.push('/register') }}
                >
                  Registrarse
                </Button>
              </>
            )}
          </li>
        </ul>
      </div>
    </header>
  )
}
