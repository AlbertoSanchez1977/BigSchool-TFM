'use client'
import { useState } from 'react'
import { LLM_MODELS, type LlmModelId } from '@/lib/config/aiScanner'

export function useLlmSelector() {
  const [selectedModel, setSelectedModel] = useState<LlmModelId>(LLM_MODELS[0].id)
  return { selectedModel, setSelectedModel }
}
