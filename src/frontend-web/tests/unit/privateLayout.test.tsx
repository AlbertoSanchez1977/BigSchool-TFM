import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'

// ── Mocks ────────────────────────────────────────────────────────────────────

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/dashboard',
}))

// Controlamos lo que devuelve useAuth para cada test
const mockUseAuth = vi.fn()
vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}))

// ── Importación tardía (después de mockear) ───────────────────────────────────
// El import debe ir DESPUÉS de los vi.mock para que los mocks estén activos
// cuando el módulo se cargue por primera vez. En Vitest, vi.mock se iza
// automáticamente, pero es buena práctica colocarlos antes del import.
import PrivateLayout from '@/app/(private)/layout'

// ── Tests ────────────────────────────────────────────────────────────────────

describe('PrivateLayout (guard)', () => {
  beforeEach(() => {
    mockPush.mockReset()
  })

  it('renderiza children cuando el usuario está autenticado', async () => {
    mockUseAuth.mockReturnValue({ user: { email: 'u@b.com', fullName: 'Test' }, isLoading: false })

    render(<PrivateLayout><span data-testid="child">contenido privado</span></PrivateLayout>)

    await waitFor(() => expect(screen.getByTestId('child')).toBeTruthy())
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('redirige a / cuando no hay sesión y ya terminó de cargar', async () => {
    mockUseAuth.mockReturnValue({ user: null, isLoading: false })

    render(<PrivateLayout><span data-testid="child">contenido privado</span></PrivateLayout>)

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith('/'))
    expect(screen.queryByTestId('child')).toBeNull()
  })

  it('no redirige mientras isLoading es true (espera a que localStorage hidrate)', () => {
    // Este test verifica que el guard no actúa en el primer render del servidor
    // (cuando isLoading=true) — evita una redirección prematura antes de leer localStorage
    mockUseAuth.mockReturnValue({ user: null, isLoading: true })

    render(<PrivateLayout><span data-testid="child">contenido privado</span></PrivateLayout>)

    expect(mockPush).not.toHaveBeenCalled()
  })
})
