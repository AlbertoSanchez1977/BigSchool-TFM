'use client'

import { useState, useRef } from 'react'
import Link from 'next/link'
import { Bot, FileText, Send, Settings, Trash2, Upload, Zap } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { LLM_MODELS, type LlmModelId } from '@/lib/config/aiScanner'
import { useLlmSelector } from './use-llm-selector'
import { useRagState } from './use-rag-state'

// ── Types ─────────────────────────────────────────────────────────────────────

interface Message {
  id: string
  role: 'user' | 'assistant'
  content: string
}

// ── Coming soon ───────────────────────────────────────────────────────────────

function ComingSoon() {
  return (
    <div className="mx-auto max-w-6xl px-5 py-10 md:px-8">
      <div className="mb-8">
        <h1 className="font-heading text-2xl font-semibold">AI Scanner</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Análisis inteligente de tu situación financiera
        </p>
      </div>

      <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed py-24 text-center">
        <div className="mb-4 flex size-16 items-center justify-center rounded-full bg-muted">
          <Zap className="size-8 text-muted-foreground" />
        </div>
        <h2 className="mb-2 text-xl font-semibold">Próximamente</h2>
        <p className="max-w-sm text-sm text-muted-foreground">
          El asistente de IA financiero está en desarrollo. Estará disponible próximamente con
          análisis inteligente de tus gastos, inversiones y objetivos.
        </p>
      </div>
    </div>
  )
}

// ── Chat panel ────────────────────────────────────────────────────────────────

interface ChatPanelProps {
  selectedModel: LlmModelId
  onModelChange: (model: LlmModelId) => void
}

function ChatPanel({ selectedModel, onModelChange }: ChatPanelProps) {
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState('')

  function sendMessage() {
    const text = input.trim()
    if (!text) return

    const userMsg: Message = { id: crypto.randomUUID(), role: 'user', content: text }
    // TODO (deuda técnica backend): POST /ai/chat { model, messages[], ragFileIds[] } → { reply }
    const assistantMsg: Message = {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: 'Demo — sin backend de IA conectado',
    }
    setMessages(prev => [...prev, userMsg, assistantMsg])
    setInput('')
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      sendMessage()
    }
  }

  return (
    <Card className="flex flex-col h-[calc(100vh-200px)] min-h-[500px]">
      {/* Header: LLM selector + settings */}
      <CardHeader className="flex flex-row items-center gap-3 pb-3">
        <Bot className="size-5 text-muted-foreground" />
        <CardTitle className="text-base flex-1">Chat</CardTitle>

        <div data-testid="llm-selector" className="flex items-center gap-2">
          <Select
            value={selectedModel}
            onValueChange={(v) => onModelChange(v as LlmModelId)}
          >
            <SelectTrigger className="h-8 text-xs">
              <SelectValue>
                {(v: LlmModelId) => LLM_MODELS.find(m => m.id === v)?.label ?? v}
              </SelectValue>
            </SelectTrigger>
            <SelectContent alignItemWithTrigger={false}>
              {LLM_MODELS.map(m => (
                <SelectItem key={m.id} value={m.id}>
                  {m.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button variant="ghost" size="icon" className="size-8 p-0">
            <Link href="/ai-scanner/settings" aria-label="Configuración de keys" className="flex items-center justify-center size-full">
              <Settings className="size-4" />
            </Link>
          </Button>
        </div>
      </CardHeader>

      {/* Message thread */}
      <CardContent className="flex-1 overflow-y-auto space-y-4 pb-0">
        {messages.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center text-center">
            <p className="text-sm text-muted-foreground">
              Escribe un mensaje para empezar.
            </p>
          </div>
        ) : (
          messages.map(msg => (
            <div
              key={msg.id}
              className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
            >
              <div
                className={`rounded-2xl px-4 py-2 text-sm max-w-[80%] ${
                  msg.role === 'user'
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-muted text-foreground'
                }`}
              >
                {msg.content}
              </div>
            </div>
          ))
        )}
      </CardContent>

      {/* Input */}
      <div className="p-4 pt-3 flex gap-2">
        <Input
          data-testid="chat-input"
          placeholder="Escribe tu pregunta…"
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          className="flex-1"
        />
        <Button onClick={sendMessage} size="icon" aria-label="Enviar mensaje">
          <Send className="size-4" />
        </Button>
      </div>
    </Card>
  )
}

// ── RAG panel ─────────────────────────────────────────────────────────────────

interface RagPanelProps {
  files: ReturnType<typeof useRagState>['files']
  addFile: (file: File) => void
  removeFile: (id: string) => void
}

function RagPanel({ files, addFile, removeFile }: RagPanelProps) {
  const inputRef = useRef<HTMLInputElement>(null)

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const picked = e.target.files
    if (!picked) return
    Array.from(picked).forEach(addFile)
    e.target.value = ''
  }

  function formatSize(bytes: number) {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  return (
    <Card data-testid="rag-panel" className="flex flex-col h-[calc(100vh-200px)] min-h-[500px]">
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Documentos RAG</CardTitle>
        <p className="text-xs text-muted-foreground">
          Sube ficheros para incluirlos en el contexto del chat.
        </p>
      </CardHeader>

      <CardContent className="flex flex-col flex-1 gap-4 overflow-hidden">
        {/* Upload zone */}
        <div>
          <input
            ref={inputRef}
            type="file"
            multiple
            className="hidden"
            onChange={handleFileChange}
            accept=".pdf,.txt,.md,.csv,.docx"
          />
          <Button
            variant="outline"
            className="w-full border-dashed gap-2"
            onClick={() => inputRef.current?.click()}
          >
            <Upload className="size-4" />
            Subir fichero
          </Button>
          {/* TODO (deuda técnica backend): POST /ai/rag/files (multipart) → { id, name, size, status } */}
          {/* TODO (deuda técnica backend): GET /ai/rag/files → RagFile[] */}
        </div>

        {/* File list */}
        <div className="flex-1 overflow-y-auto space-y-2">
          {files.length === 0 ? (
            <div className="flex h-32 flex-col items-center justify-center rounded-lg border border-dashed text-center">
              <FileText className="mb-2 size-6 text-muted-foreground" />
              <p className="text-xs text-muted-foreground">
                No hay ficheros subidos todavía.
              </p>
            </div>
          ) : (
            files.map(f => (
              <div
                key={f.id}
                className="flex items-center justify-between rounded-lg border bg-muted/30 px-3 py-2 gap-2"
              >
                <FileText className="size-4 shrink-0 text-muted-foreground" />
                <div className="flex-1 min-w-0">
                  <p className="truncate text-xs font-medium">{f.name}</p>
                  <p className="text-xs text-muted-foreground">
                    {formatSize(f.size)} · <Badge variant="outline" className="text-xs py-0">{f.status === 'pending' ? 'Pendiente de indexar' : f.status}</Badge>
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  className="size-7 shrink-0 text-muted-foreground hover:text-destructive"
                  onClick={() => removeFile(f.id)}
                  aria-label={`Eliminar ${f.name}`}
                >
                  <Trash2 className="size-3.5" />
                </Button>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  )
}

// ── Main view ─────────────────────────────────────────────────────────────────

interface AiScannerViewProps {
  enabled: boolean
}

export default function AiScannerView({ enabled }: AiScannerViewProps) {
  if (!enabled) return <ComingSoon />

  return <AiScannerContent />
}

function AiScannerContent() {
  const { selectedModel, setSelectedModel } = useLlmSelector()
  const { files, addFile, removeFile } = useRagState()

  return (
    <div className="mx-auto max-w-6xl px-5 py-10 md:px-8">
      <div className="mb-6">
        <h1 className="font-heading text-2xl font-semibold">AI Scanner</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Análisis inteligente de tu situación financiera
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <ChatPanel selectedModel={selectedModel} onModelChange={setSelectedModel} />
        <RagPanel files={files} addFile={addFile} removeFile={removeFile} />
      </div>
    </div>
  )
}
