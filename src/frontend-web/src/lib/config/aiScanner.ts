// Toggle: controlado por variable de entorno para no hardcodear en código.
// NEXT_PUBLIC_ es obligatorio para que Next.js exponga la variable al navegador.
// Default: false (no hay backend de IA aún — habilitar solo para demo local).
export const AI_SCANNER_ENABLED =
  process.env.NEXT_PUBLIC_AI_SCANNER_ENABLED === 'true'

export const LLM_MODELS = [
  { id: 'claude',  label: 'Claude (Anthropic)' },
  { id: 'gpt',     label: 'GPT (OpenAI)' },
  { id: 'gemini',  label: 'Gemini (Google)' },
] as const

export type LlmModelId = (typeof LLM_MODELS)[number]['id']
