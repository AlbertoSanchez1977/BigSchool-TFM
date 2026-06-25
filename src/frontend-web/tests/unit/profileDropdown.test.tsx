import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'

// ── Mocks ────────────────────────────────────────────────────────────────────

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

const mockLogout = vi.fn()
const mockUseAuth = vi.fn()
vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}))

import { ProfileDropdown } from '@/components/auth/profile-dropdown'

// ── Tests ────────────────────────────────────────────────────────────────────

const testUser = { email: 'alberto@bigschool.com', fullName: 'Alberto Sánchez' }

describe('ProfileDropdown', () => {
  beforeEach(() => {
    mockPush.mockReset()
    mockLogout.mockReset()
    mockUseAuth.mockReturnValue({ user: testUser, logout: mockLogout })
  })

  it('renderiza el avatar/botón con las iniciales del nombre', () => {
    render(<ProfileDropdown />)
    // Las iniciales se extraen del fullName: "Alberto Sánchez" → "AS"
    expect(screen.getByRole('button', { name: /alberto/i })).toBeTruthy()
  })

  it('muestra nombre y email del usuario al abrir', () => {
    render(<ProfileDropdown />)
    fireEvent.click(screen.getByRole('button', { name: /alberto/i }))
    expect(screen.getByText(testUser.fullName)).toBeTruthy()
    expect(screen.getByText(testUser.email)).toBeTruthy()
  })

  it('llama a logout al hacer clic en "Cerrar sesión"', () => {
    render(<ProfileDropdown />)
    fireEvent.click(screen.getByRole('button', { name: /alberto/i }))
    fireEvent.click(screen.getByText(/cerrar sesión/i))
    expect(mockLogout).toHaveBeenCalledOnce()
  })

  it('navega a /profile al hacer clic en "Editar perfil"', () => {
    render(<ProfileDropdown />)
    fireEvent.click(screen.getByRole('button', { name: /alberto/i }))
    fireEvent.click(screen.getByText(/editar perfil/i))
    expect(mockPush).toHaveBeenCalledWith('/profile')
  })
})
