import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, renderHook, act } from '@testing-library/react'

// ── Mocks de navegación ───────────────────────────────────────────────────────
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/ai-scanner',
}))

import { LLM_MODELS } from '@/lib/config/aiScanner'
import { useLlmSelector } from '@/components/ai-scanner/use-llm-selector'
import { useRagState } from '@/components/ai-scanner/use-rag-state'
import AiScannerView from '@/components/ai-scanner/ai-scanner-view'

// ── LLM_MODELS config ─────────────────────────────────────────────────────────

describe('LLM_MODELS', () => {
  it('contiene al menos 3 modelos', () => {
    expect(LLM_MODELS.length).toBeGreaterThanOrEqual(3)
  })

  it('cada modelo tiene id y label', () => {
    LLM_MODELS.forEach(m => {
      expect(m).toHaveProperty('id')
      expect(m).toHaveProperty('label')
    })
  })

  it('incluye claude, gpt y gemini', () => {
    const ids = LLM_MODELS.map(m => m.id)
    expect(ids).toContain('claude')
    expect(ids).toContain('gpt')
    expect(ids).toContain('gemini')
  })
})

// ── useLlmSelector ────────────────────────────────────────────────────────────

describe('useLlmSelector', () => {
  it('comienza con el primer modelo de la lista', () => {
    const { result } = renderHook(() => useLlmSelector())
    expect(result.current.selectedModel).toBe(LLM_MODELS[0].id)
  })

  it('cambia el modelo seleccionado', () => {
    const { result } = renderHook(() => useLlmSelector())
    act(() => result.current.setSelectedModel('gpt'))
    expect(result.current.selectedModel).toBe('gpt')
  })

  it('el modelo seleccionado siempre es un id válido de la lista', () => {
    const { result } = renderHook(() => useLlmSelector())
    const ids = LLM_MODELS.map(m => m.id)
    expect(ids).toContain(result.current.selectedModel)
  })
})

// ── useRagState ───────────────────────────────────────────────────────────────

describe('useRagState', () => {
  it('empieza con lista vacía', () => {
    const { result } = renderHook(() => useRagState())
    expect(result.current.files).toHaveLength(0)
  })

  it('añade un fichero a la lista', () => {
    const { result } = renderHook(() => useRagState())
    const file = new File(['contenido'], 'informe.pdf', { type: 'application/pdf' })
    act(() => result.current.addFile(file))
    expect(result.current.files).toHaveLength(1)
    expect(result.current.files[0].name).toBe('informe.pdf')
  })

  it('el fichero añadido tiene estado "pending"', () => {
    const { result } = renderHook(() => useRagState())
    act(() => result.current.addFile(new File(['x'], 'doc.pdf')))
    expect(result.current.files[0].status).toBe('pending')
  })

  it('elimina un fichero por id', () => {
    const { result } = renderHook(() => useRagState())
    act(() => result.current.addFile(new File(['x'], 'doc.pdf')))
    const id = result.current.files[0].id
    act(() => result.current.removeFile(id))
    expect(result.current.files).toHaveLength(0)
  })

  it('solo elimina el fichero con el id indicado', () => {
    const { result } = renderHook(() => useRagState())
    act(() => {
      result.current.addFile(new File(['a'], 'primero.pdf'))
      result.current.addFile(new File(['b'], 'segundo.pdf'))
    })
    const idPrimero = result.current.files[0].id
    act(() => result.current.removeFile(idPrimero))
    expect(result.current.files).toHaveLength(1)
    expect(result.current.files[0].name).toBe('segundo.pdf')
  })
})

// ── AiScannerView toggle ──────────────────────────────────────────────────────

describe('AiScannerView', () => {
  beforeEach(() => vi.clearAllMocks())

  it('muestra pantalla Próximamente cuando enabled=false', () => {
    render(<AiScannerView enabled={false} />)
    expect(screen.getByRole('heading', { name: /próximamente/i })).toBeTruthy()
  })

  it('no muestra el chat-input cuando enabled=false', () => {
    render(<AiScannerView enabled={false} />)
    expect(screen.queryByTestId('chat-input')).toBeNull()
  })

  it('muestra el chat-input cuando enabled=true', () => {
    render(<AiScannerView enabled={true} />)
    expect(screen.getByTestId('chat-input')).toBeTruthy()
  })

  it('muestra el panel RAG cuando enabled=true', () => {
    render(<AiScannerView enabled={true} />)
    expect(screen.getByTestId('rag-panel')).toBeTruthy()
  })

  it('muestra el selector de LLM cuando enabled=true', () => {
    render(<AiScannerView enabled={true} />)
    expect(screen.getByTestId('llm-selector')).toBeTruthy()
  })
})
