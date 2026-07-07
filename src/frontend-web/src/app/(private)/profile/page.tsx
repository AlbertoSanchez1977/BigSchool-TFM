'use client'

import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { CheckCircle2, UserPen } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/hooks/useAuth'
import { useProfile, useUpdateProfile } from '@/hooks/useProfile'
import { profileSchema, type ProfileFormValues } from '@/lib/schemas/profile'
import { ApiError } from '@/lib/apiClient'

// ── Fila de información de solo lectura ───────────────────────────────────────

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[140px_1fr] items-center gap-2">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-sm font-medium">{value}</span>
    </div>
  )
}

function formatLastLogin(iso: string | null): string {
  if (!iso) return 'Nunca'
  return new Date(iso).toLocaleString('es-ES', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

// ── Página ────────────────────────────────────────────────────────────────────

export default function ProfilePage() {
  const { updateFullName } = useAuth()
  const { data: profile, isLoading, isError } = useProfile()
  const updateProfile = useUpdateProfile()
  const [saved, setSaved] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: '', password: '', confirmPassword: '' },
  })

  // El formulario se rellena cuando llegan los datos reales (useProfile es async;
  // en el primer render aún no hay `profile`, así que no puede ir en defaultValues).
  useEffect(() => {
    if (profile) reset({ fullName: profile.fullName, password: '', confirmPassword: '' })
  }, [profile, reset])

  function onSubmit(values: ProfileFormValues) {
    setSaved(false)
    updateProfile.mutate(
      { fullName: values.fullName, password: values.password || null },
      {
        onSuccess: (updated) => {
          // ProfileDropdown/navbar leen el fullName de AuthProvider (localStorage +
          // memoria), no de esta query — sin esto seguirían mostrando el nombre viejo
          // hasta el próximo login/refresh de token.
          updateFullName(updated.fullName)
          setSaved(true)
          reset({ fullName: updated.fullName, password: '', confirmPassword: '' })
        },
        onError: (e) => toast.error(e instanceof ApiError ? e.message : 'Error al guardar los cambios'),
      },
    )
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
            {isLoading && (
              <div className="space-y-2.5" data-testid="loading-state">
                {Array.from({ length: 3 }).map((_, i) => (
                  <Skeleton key={i} className="h-4 w-2/3" />
                ))}
              </div>
            )}

            {!isLoading && isError && (
              <p data-testid="error-state" className="text-sm text-destructive">
                No se pudo cargar tu perfil. Inténtalo de nuevo más tarde.
              </p>
            )}

            {!isLoading && !isError && profile && (
              <>
                <InfoRow label="Email" value={profile.email} />
                <InfoRow label="Moneda base" value={profile.baseCurrency} />
                <InfoRow label="Último acceso" value={formatLastLogin(profile.lastLoginDate)} />
              </>
            )}
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
                  disabled={isSubmitting || updateProfile.isPending}
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
