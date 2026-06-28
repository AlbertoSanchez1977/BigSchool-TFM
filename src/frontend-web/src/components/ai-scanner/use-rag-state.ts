'use client'
import { useState } from 'react'

export interface RagFile {
  id: string
  name: string
  size: number
  status: 'pending'
}

export function useRagState() {
  const [files, setFiles] = useState<RagFile[]>([])

  function addFile(file: File) {
    // TODO (deuda técnica backend): POST /ai/rag/files (multipart) → { id, name, size, status }
    setFiles(prev => [
      ...prev,
      { id: crypto.randomUUID(), name: file.name, size: file.size, status: 'pending' },
    ])
  }

  function removeFile(id: string) {
    // TODO (deuda técnica backend): DELETE /ai/rag/files/{id}
    setFiles(prev => prev.filter(f => f.id !== id))
  }

  return { files, addFile, removeFile }
}
