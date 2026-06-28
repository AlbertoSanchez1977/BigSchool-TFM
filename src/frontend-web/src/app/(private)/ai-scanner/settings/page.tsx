'use client'

import { useEffect } from 'react'
import Link from 'next/link'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft, KeyRound, AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { apiKeySchema, type ApiKeyFormValues } from '@/lib/schemas/aiScanner'
import { LLM_MODELS, type LlmModelId } from '@/lib/config/aiScanner'

// ── localStorage helpers ──────────────────────────────────────────────────────
// TODO (deuda técnica backend): PUT /ai/keys { provider, key } — en producción,
// las keys se cifran en servidor y nunca viajan en claro al backend.
// Aquí se persisten solo en localStorage como demo local.

const LS_PREFIX = 'ai_key_'

function saveKey(provider: string, key: string) {
  localStorage.setItem(`${LS_PREFIX}${provider}`, key)
}

function loadKey(provider: string): string {
  return localStorage.getItem(`${LS_PREFIX}${provider}`) ?? ''
}

// ── Key form per provider ─────────────────────────────────────────────────────

interface ProviderFormProps {
  model: (typeof LLM_MODELS)[number]
}

function ProviderKeyForm({ model }: ProviderFormProps) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitSuccessful },
  } = useForm<ApiKeyFormValues>({
    resolver: zodResolver(apiKeySchema),
    defaultValues: { provider: model.id, key: '' },
  })

  useEffect(() => {
    reset({ provider: model.id, key: loadKey(model.id) })
  }, [model.id, reset])

  function onSubmit(values: ApiKeyFormValues) {
    saveKey(values.provider, values.key)
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
      <input type="hidden" {...register('provider')} />

      <div className="space-y-1">
        <Label htmlFor={`key-${model.id}`}>
          API Key — {model.label}
        </Label>
        <Input
          id={`key-${model.id}`}
          type="password"
          placeholder="sk-…"
          {...register('key')}
          aria-invalid={!!errors.key}
        />
        {errors.key && (
          <p className="text-xs text-destructive">{errors.key.message}</p>
        )}
      </div>

      <Button type="submit" size="sm" variant="outline">
        Guardar
      </Button>

      {isSubmitSuccessful && (
        <p className="text-xs text-positive">Guardado localmente ✓</p>
      )}
    </form>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function AiScannerSettingsPage() {
  return (
    <div className="mx-auto max-w-2xl px-5 py-10 md:px-8">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2">
        <Link
          href="/ai-scanner"
          className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeft className="size-4" />
          AI Scanner
        </Link>
        <span className="text-muted-foreground">·</span>
        <span className="text-sm font-medium">Configuración de keys</span>
      </div>

      <div className="mb-6 flex items-start gap-3">
        <div className="flex size-10 shrink-0 items-center justify-center rounded-full bg-muted">
          <KeyRound className="size-5 text-muted-foreground" />
        </div>
        <div>
          <h1 className="font-heading text-xl font-semibold">Configuración de API Keys</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Introduce tus claves de acceso a los proveedores de LLM.
          </p>
        </div>
      </div>

      {/* Warning */}
      <div className="mb-6 flex items-start gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 dark:border-amber-900 dark:bg-amber-950/30">
        <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-600 dark:text-amber-500" />
        <p className="text-xs text-amber-700 dark:text-amber-400">
          <strong>Solo demo local.</strong> Las API keys se guardan únicamente en tu navegador
          (localStorage) y nunca se envían a ningún servidor. En producción, las claves se
          cifrarían en servidor y nunca viajarían en claro.
        </p>
      </div>

      {/* One card per LLM provider */}
      <div className="space-y-4">
        {LLM_MODELS.map(model => (
          <Card key={model.id}>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium">{model.label}</CardTitle>
            </CardHeader>
            <CardContent>
              <ProviderKeyForm model={model} />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
