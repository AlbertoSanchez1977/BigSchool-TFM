'use client'

import { useState } from 'react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { LineChart, Menu, X, LayoutDashboard, Wallet, TrendingUp, Building2, ScanLine } from 'lucide-react'
import { ProfileDropdown } from '@/components/auth/profile-dropdown'
import { cn } from '@/lib/utils'

const navItems = [
  { href: '/dashboard',   label: 'Dashboard',         icon: LayoutDashboard },
  { href: '/expenses',    label: 'Gastos e ingresos', icon: Wallet },
  { href: '/investments', label: 'Inversiones',        icon: TrendingUp },
  { href: '/market',      label: 'Mercado',            icon: Building2 },
  { href: '/ai-scanner',  label: 'AI Scanner',         icon: ScanLine },
]

export function NavbarPrivate() {
  const pathname = usePathname()
  const [mobileOpen, setMobileOpen] = useState(false)

  return (
    <header className="sticky top-0 z-50 w-full border-b border-border bg-background/80 backdrop-blur-md">
      <nav className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between px-5 md:px-8">

        {/* ── Logo → vuelve a la landing ──────────────────────────────────── */}
        <Link href="/" className="flex items-center gap-2" aria-label="BigSchool inicio">
          <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <LineChart className="h-5 w-5" aria-hidden="true" />
          </span>
          <span className="font-heading text-lg font-semibold tracking-tight">BigSchool</span>
        </Link>

        {/* ── Links de navegación (solo desktop) ──────────────────────────── */}
        <ul className="hidden items-center gap-1 md:flex">
          {navItems.map(({ href, label, icon: Icon }) => {
            const active = pathname === href || pathname.startsWith(href + '/')
            return (
              <li key={href}>
                <Link
                  href={href}
                  className={cn(
                    'flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                    active
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                  )}
                  aria-current={active ? 'page' : undefined}
                >
                  <Icon className="h-4 w-4" aria-hidden="true" />
                  {label}
                </Link>
              </li>
            )
          })}
        </ul>

        {/* ── Zona derecha desktop: ProfileDropdown ───────────────────────── */}
        <div className="hidden md:flex">
          <ProfileDropdown />
        </div>

        {/* ── Botón hamburguesa (solo móvil) ──────────────────────────────── */}
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

      {/* ── Menú móvil expandido ─────────────────────────────────────────── */}
      <div className={cn('border-t border-border md:hidden', mobileOpen ? 'block' : 'hidden')}>
        <ul className="mx-auto flex w-full max-w-6xl flex-col gap-1 px-5 py-3">
          {navItems.map(({ href, label, icon: Icon }) => {
            const active = pathname === href || pathname.startsWith(href + '/')
            return (
              <li key={href}>
                <Link
                  href={href}
                  onClick={() => setMobileOpen(false)}
                  className={cn(
                    'flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    active
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                  )}
                  aria-current={active ? 'page' : undefined}
                >
                  <Icon className="h-4 w-4" aria-hidden="true" />
                  {label}
                </Link>
              </li>
            )
          })}
        </ul>
        {/* ProfileDropdown accesible también en móvil */}
        <div className="border-t border-border px-5 py-3">
          <ProfileDropdown />
        </div>
      </div>
    </header>
  )
}
