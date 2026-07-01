'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useRouter } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/hooks/useAuth'
import { loginSchema, type LoginFormValues } from '@/lib/schemas/auth'
import { ApiError } from '@/lib/apiClient'

interface LoginFormProps {
  onSwitchMode: () => void
  onSuccess: () => void
}

export function LoginForm({ onSwitchMode, onSuccess }: LoginFormProps) {
  const { login } = useAuth()
  const router = useRouter()
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<LoginFormValues>({ resolver: zodResolver(loginSchema) })

  async function onSubmit(data: LoginFormValues) {
    try {
      await login(data)
      onSuccess()
      router.push('/dashboard')
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Error al iniciar sesión'
      setError('root', { message })
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
      <div className="space-y-1">
        <h2 className="text-lg font-semibold">Iniciar sesión</h2>
        <p className="text-sm text-muted-foreground">Accede a tu cuenta de BigSchool</p>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="login-email">Email</Label>
        <Input
          id="login-email"
          type="email"
          autoComplete="email"
          placeholder="tu@email.com"
          {...register('email')}
        />
        {errors.email && (
          <p className="text-xs text-destructive">{errors.email.message}</p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="login-password">Contraseña</Label>
        <Input
          id="login-password"
          type="password"
          autoComplete="current-password"
          placeholder="••••••••"
          {...register('password')}
        />
        {errors.password && (
          <p className="text-xs text-destructive">{errors.password.message}</p>
        )}
      </div>

      {errors.root && (
        <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {errors.root.message}
        </p>
      )}

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? 'Entrando…' : 'Entrar'}
      </Button>

      <p className="text-center text-sm text-muted-foreground">
        ¿No tienes cuenta?{' '}
        <button
          type="button"
          onClick={onSwitchMode}
          className="font-medium text-primary hover:underline"
        >
          Regístrate
        </button>
      </p>
    </form>
  )
}
