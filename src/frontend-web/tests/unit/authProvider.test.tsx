import { render, screen, act, waitFor, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest'
import { AuthProvider, useAuth, REFRESH_CHECK_INTERVAL_MS } from '@/lib/auth/AuthProvider'
import { tokenStore, type TokenData } from '@/lib/auth/tokenStore'
import { registerUnauthorizedHandler } from '@/lib/api'

// ── Mocks ────────────────────────────────────────────────────────────────────

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

// Sustituimos fetch global; todas las llamadas de `api` pasan por aquí
const mockFetch = vi.fn()
vi.stubGlobal('fetch', mockFetch)

// ── Helpers ──────────────────────────────────────────────────────────────────

function makeEnvelope<T>(data: T) {
  return { data, errors: [], meta: null }
}

function buildAuthResponse(overrides: Partial<TokenData & { accessToken: string }> = {}) {
  return {
    accessToken: 'tok_test',
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    email: 'user@bigschool.com',
    fullName: 'Test User',
    ...overrides,
  }
}

function setupFetchOk(data: unknown) {
  mockFetch.mockResolvedValueOnce({
    ok: true,
    status: 200,
    json: async () => makeEnvelope(data),
  })
}

function setupFetchError(status = 401) {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status,
    json: async () => ({
      data: null,
      errors: [{ code: 'UNAUTHORIZED', message: 'Token inválido' }],
      meta: null,
    }),
  })
}

// Componente auxiliar que expone el contexto en el DOM para assertions
function AuthConsumer() {
  const { user, token, isLoading, login, logout, refresh } = useAuth()
  return (
    <div>
      <span data-testid="loading">{isLoading ? 'loading' : 'ready'}</span>
      <span data-testid="email">{user?.email ?? 'none'}</span>
      <span data-testid="fullName">{user?.fullName ?? 'none'}</span>
      <span data-testid="token">{token ?? 'none'}</span>
      <button onClick={() => void login({ email: 'a@b.com', password: 'pass123' })}>
        Login
      </button>
      <button onClick={logout}>Logout</button>
      <button onClick={() => void refresh()}>Refresh</button>
    </div>
  )
}

function Wrapper({ children }: { children: React.ReactNode }) {
  return <AuthProvider>{children}</AuthProvider>
}

// ── Tests ────────────────────────────────────────────────────────────────────

describe('AuthProvider', () => {
  beforeEach(() => {
    localStorage.clear()
    mockFetch.mockReset()
    mockPush.mockReset()
    registerUnauthorizedHandler(() => {}) // aislar estado del módulo entre tests
  })

  // ── Estado inicial ──────────────────────────────────────────────────────────

  describe('estado inicial', () => {
    it('arranca con user y token null cuando no hay sesión guardada', async () => {
      render(<AuthConsumer />, { wrapper: Wrapper })
      await waitFor(() => expect(screen.getByTestId('loading').textContent).toBe('ready'))
      expect(screen.getByTestId('email').textContent).toBe('none')
      expect(screen.getByTestId('token').textContent).toBe('none')
    })

    it('hidrata el estado desde localStorage al montar', async () => {
      tokenStore.save({
        accessToken: 'stored_tok',
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        email: 'stored@b.com',
        fullName: 'Stored User',
        sessionStartedAt: Date.now() - 10 * 60 * 1000,
        refreshCount: 1,
      })
      render(<AuthConsumer />, { wrapper: Wrapper })
      await waitFor(() => expect(screen.getByTestId('email').textContent).toBe('stored@b.com'))
      expect(screen.getByTestId('token').textContent).toBe('stored_tok')
      expect(screen.getByTestId('fullName').textContent).toBe('Stored User')
    })
  })

  // ── Acciones ────────────────────────────────────────────────────────────────

  describe('login', () => {
    it('guarda token + user en estado y en localStorage, con refreshCount=0', async () => {
      const authResp = buildAuthResponse()
      setupFetchOk(authResp)
      render(<AuthConsumer />, { wrapper: Wrapper })
      await waitFor(() => expect(screen.getByTestId('loading').textContent).toBe('ready'))

      fireEvent.click(screen.getByText('Login'))

      await waitFor(() => expect(screen.getByTestId('email').textContent).toBe(authResp.email))
      expect(screen.getByTestId('token').textContent).toBe(authResp.accessToken)

      const stored = tokenStore.load()
      expect(stored?.email).toBe(authResp.email)
      expect(stored?.refreshCount).toBe(0)
      expect(stored?.sessionStartedAt).toBeGreaterThan(0)
    })
  })

  describe('logout', () => {
    it('limpia user, token y localStorage', async () => {
      tokenStore.save({
        accessToken: 'tok',
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        email: 'u@b.com',
        fullName: 'User',
        sessionStartedAt: Date.now(),
        refreshCount: 0,
      })
      render(<AuthConsumer />, { wrapper: Wrapper })
      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('tok'))

      fireEvent.click(screen.getByText('Logout'))

      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('none'))
      expect(screen.getByTestId('email').textContent).toBe('none')
      expect(tokenStore.load()).toBeNull()
    })
  })

  describe('refresh', () => {
    it('llama POST /auth/refresh, incrementa refreshCount y actualiza el token', async () => {
      tokenStore.save({
        accessToken: 'old_tok',
        expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        email: 'u@b.com',
        fullName: 'User',
        sessionStartedAt: Date.now() - 30 * 60 * 1000,
        refreshCount: 1,
      })
      const newAuth = buildAuthResponse({ accessToken: 'new_tok' })
      setupFetchOk(newAuth)

      render(<AuthConsumer />, { wrapper: Wrapper })
      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('old_tok'))

      fireEvent.click(screen.getByText('Refresh'))

      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('new_tok'))
      expect(tokenStore.load()?.refreshCount).toBe(2) // 1 → 2
    })

    it('llama logout y redirige a /login si POST /auth/refresh falla (401)', async () => {
      tokenStore.save({
        accessToken: 'old_tok',
        expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        email: 'u@b.com',
        fullName: 'User',
        sessionStartedAt: Date.now(),
        refreshCount: 0,
      })
      setupFetchError(401)

      render(<AuthConsumer />, { wrapper: Wrapper })
      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('old_tok'))

      fireEvent.click(screen.getByText('Refresh'))

      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('none'))
      expect(mockPush).toHaveBeenCalledWith('/login')
    })
  })

  // ── Timer proactivo ─────────────────────────────────────────────────────────
  // Estos tests usan fake timers para simular el paso del tiempo sin esperar 30 s reales.
  // El timer se dispara cada REFRESH_CHECK_INTERVAL_MS y comprueba si el token está próximo
  // a caducar. Si shouldRefreshSoon=true: llama refresh (si canRefresh) o logout (si no).

  describe('timer proactivo de refresco', () => {
    afterEach(() => {
      vi.useRealTimers()
    })

    it('llama a refresh cuando el token expira pronto y canRefresh=true', async () => {
      // Solo falsear setInterval/clearInterval para que los timers del test sean controlables
      // pero dejar setTimeout real → waitFor (que usa setTimeout internamente) sigue funcionando
      vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
      const now = Date.now()
      // Token expira en 4 min → dentro del margen de 5 min → shouldRefreshSoon = true
      const expiresAt = new Date(now + 4 * 60 * 1000).toISOString()

      tokenStore.save({
        accessToken: 'near_expiry',
        expiresAt,
        email: 'u@b.com',
        fullName: 'User',
        sessionStartedAt: now - 30 * 60 * 1000, // 30 min de sesión, dentro del límite 24 h
        refreshCount: 0,                          // 0 refrescos, dentro del límite de 5
      })
      const newAuth = buildAuthResponse({ accessToken: 'refreshed' })
      setupFetchOk(newAuth)

      render(<AuthConsumer />, { wrapper: Wrapper })
      // waitFor usa setTimeout real → funciona aunque setInterval esté falseado
      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('near_expiry'))

      // Disparar el intervalo de 30 s y drenar la cola de microtareas del fetch mockeado
      await act(async () => {
        vi.advanceTimersByTime(REFRESH_CHECK_INTERVAL_MS + 1)
        await Promise.resolve()
        await Promise.resolve()
      })

      // El timer debe haber llamado al endpoint de refresh
      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/auth/refresh'),
        expect.anything(),
      )
    })

    it('llama logout y redirige cuando no se puede refrescar (refreshCount >= MAX)', async () => {
      vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
      const now = Date.now()
      const expiresAt = new Date(now + 4 * 60 * 1000).toISOString()

      tokenStore.save({
        accessToken: 'near_expiry',
        expiresAt,
        email: 'u@b.com',
        fullName: 'User',
        sessionStartedAt: now - 30 * 60 * 1000,
        refreshCount: 5, // MAX_REFRESHES = 5 → canRefresh = false
      })

      render(<AuthConsumer />, { wrapper: Wrapper })
      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('near_expiry'))

      await act(async () => {
        vi.advanceTimersByTime(REFRESH_CHECK_INTERVAL_MS + 1)
        await Promise.resolve()
        await Promise.resolve()
      })

      // logout() es síncrono → el estado ya está en none tras el act
      await waitFor(() => expect(screen.getByTestId('token').textContent).toBe('none'))
      expect(mockPush).toHaveBeenCalledWith('/login')
    })
  })
})
