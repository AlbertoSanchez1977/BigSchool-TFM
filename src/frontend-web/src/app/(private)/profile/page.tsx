'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { CheckCircle2, UserPen } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { useAuth } from '@/hooks/useAuth'
import { profileSchema, type ProfileFormValues } from '@/lib/schemas/profile'

// ── Contratos de backend (deuda técnica) ──────────────────────────────────────
// GET  /users/me → { idUser, email, fullName, baseCurrency, lastLoginDate }
//   Mock actual: datos derivados de useAuth (email, fullName) + placeholders.
// PUT  /users/me { fullName, password? } → UserProfile
//   Sin backend: feedback optimista; no persiste fuera de la sesión actual.

// ── Fila de información de solo lectura ───────────────────────────────────────

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[140px_1fr] items-center gap-2">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-sm font-medium">{value}</span>
    </div>
  )
}

// ── Página ────────────────────────────────────────────────────────────────────

export default function ProfilePage() {
  const { user } = useAuth()
  const [saved, setSaved] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      fullName: user?.fullName ?? '',
      password: '',
      confirmPassword: '',
    },
  })

  function onSubmit(_values: ProfileFormValues) {
    // TODO (deuda técnica backend): PUT /users/me { fullName, password? }
    // En producción actualiza BD y devuelve el UserProfile actualizado.
    // Aquí solo mostramos feedback optimista.
    setSaved(true)
    reset({ fullName: _values.fullName, password: '', confirmPassword: '' })
  }

  return (
    <div className="mx-auto max-w-2xl px-5 py-10 md:px-8">
      <div className="mb-8 flex items-center gap-3">
        <div className="flex size-10 shrink-0 items-center justify-center rounded-full bg-muted">
          <UserPen className="size-5 text-muted-foreground" />
        </div>
        <div>
          <h1 className="font-heading text-2xl font-semibold">Perfil</h1>
          <p className="text-sm text-muted-foreground">Gestiona los datos de tu cuenta.</p>
        </div>
      </div>

      <div className="space-y-6">
        {/* ── Información de solo lectura ──────────────────────────────── */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Información de la cuenta
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <InfoRow label="Email" value={user?.email ?? '—'} />
            {/* TODO (deuda técnica backend): baseCurrency y lastLoginDate
                vienen de GET /users/me → { idUser, email, fullName, baseCurrency, lastLoginDate } */}
            <InfoRow label="Moneda base" value={user?.currency ?? 'EUR'} />
            <InfoRow label="Último acceso" value="Pendiente de backend" />
          </CardContent>
        </Card>

        {/* ── Formulario de edición ────────────────────────────────────── */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Editar datos
            </CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">

              {/* Nombre completo */}
              <div className="space-y-1.5">
                <Label htmlFor="fullName">Nombre completo</Label>
                <Input
                  id="fullName"
                  placeholder="Tu nombre"
                  aria-invalid={!!errors.fullName}
                  {...register('fullName')}
                />
                {errors.fullName && (
                  <p className="text-xs text-destructive">{errors.fullName.message}</p>
                )}
              </div>

              <Separator />

              <p className="text-xs text-muted-foreground">
                Deja los campos de contraseña vacíos si no quieres cambiarla.
              </p>

              {/* Nueva contraseña */}
              <div className="space-y-1.5">
                <Label htmlFor="password">Nueva contraseña</Label>
                <Input
                  id="password"
                  type="password"
                  placeholder="Mínimo 8 caracteres"
                  aria-invalid={!!errors.password}
                  {...register('password')}
                />
                {errors.password && (
                  <p className="text-xs text-destructive">{errors.password.message}</p>
                )}
              </div>

              {/* Confirmar contraseña */}
              <div className="space-y-1.5">
                <Label htmlFor="confirmPassword">Confirmar contraseña</Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  placeholder="Repite la contraseña"
                  aria-invalid={!!errors.confirmPassword}
                  {...register('confirmPassword')}
                />
                {errors.confirmPassword && (
                  <p className="text-xs text-destructive">{errors.confirmPassword.message}</p>
                )}
              </div>

              <div className="flex items-center gap-4">
                <Button
                  type="submit"
                  className="gap-2"
                  disabled={isSubmitting}
                >
                  Guardar cambios
                </Button>

                {saved && (
                  <span className="flex items-center gap-1.5 text-sm text-positive">
                    <CheckCircle2 className="size-4" />
                    Datos guardados
                  </span>
                )}
              </div>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
