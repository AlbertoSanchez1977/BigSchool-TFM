// Toggle local: true = pantalla completa; false = "Próximamente".
// No hay backend de IA aún — habilitar solo para demo local.
export const AI_SCANNER_ENABLED = false

export const LLM_MODELS = [
  { id: 'claude',  label: 'Claude (Anthropic)' },
  { id: 'gpt',     label: 'GPT (OpenAI)' },
  { id: 'gemini',  label: 'Gemini (Google)' },
] as const

export type LlmModelId = (typeof LLM_MODELS)[number]['id']
