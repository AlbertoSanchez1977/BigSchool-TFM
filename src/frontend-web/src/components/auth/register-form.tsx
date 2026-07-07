'use client'

import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useRouter } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'
import { useAuth } from '@/hooks/useAuth'
import { registerSchema, type RegisterFormValues } from '@/lib/schemas/auth'
import { ApiError } from '@/lib/apiClient'
import { CURRENCIES } from '@/types/enums'

interface RegisterFormProps {
  onSwitchMode: () => void
  onSuccess: () => void
}

export function RegisterForm({ onSwitchMode, onSuccess }: RegisterFormProps) {
  const { register: registerUser } = useAuth()
  const router = useRouter()
  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<RegisterFormValues>({ resolver: zodResolver(registerSchema) })

  async function onSubmit(data: RegisterFormValues) {
    try {
      await registerUser({
        email: data.email,
        password: data.password,
        fullName: data.fullName,
        baseCurrency: data.baseCurrency,
      })
      onSuccess()
      router.push('/dashboard')
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Error al crear la cuenta'
      setError('root', { message })
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
      <div className="space-y-1">
        <h2 className="text-lg font-semibold">Crear cuenta</h2>
        <p className="text-sm text-muted-foreground">Empieza gratis en BigSchool</p>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="reg-fullName">Nombre completo</Label>
        <Input
          id="reg-fullName"
          type="text"
          autoComplete="name"
          placeholder="Tu nombre"
          {...register('fullName')}
        />
        {errors.fullName && (
          <p className="text-xs text-destructive">{errors.fullName.message}</p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="reg-email">Email</Label>
        <Input
          id="reg-email"
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
        <Label htmlFor="reg-baseCurrency">Moneda base</Label>
        <Controller
          control={control}
          name="baseCurrency"
          render={({ field }) => (
            // value nunca debe ser `undefined`: en el primer render (sin moneda
            // elegida) React trataría el Select como "no controlado" y, al elegir
            // una moneda, saltaría el aviso de "cambiar de no controlado a
            // controlado". '' no coincide con ningún <SelectItem>, así que sigue
            // sin haber preselección — solo evita el undefined inicial.
            <Select value={field.value ?? ''} onValueChange={field.onChange}>
              <SelectTrigger id="reg-baseCurrency" className="w-full" data-testid="select-baseCurrency">
                <SelectValue placeholder="Selecciona una moneda" />
              </SelectTrigger>
              <SelectContent alignItemWithTrigger={false}>
                {CURRENCIES.map((c) => (
                  <SelectItem key={c} value={c}>{c}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
        {errors.baseCurrency && (
          <p className="text-xs text-destructive">{errors.baseCurrency.message}</p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="reg-password">Contraseña</Label>
        <Input
          id="reg-password"
          type="password"
          autoComplete="new-password"
          placeholder="Mínimo 8 caracteres"
          {...register('password')}
        />
        {errors.password && (
          <p className="text-xs text-destructive">{errors.password.message}</p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="reg-confirm">Repetir contraseña</Label>
        <Input
          id="reg-confirm"
          type="password"
          autoComplete="new-password"
          placeholder="••••••••"
          {...register('confirmPassword')}
        />
        {errors.confirmPassword && (
          <p className="text-xs text-destructive">{errors.confirmPassword.message}</p>
        )}
      </div>

      {errors.root && (
        <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {errors.root.message}
        </p>
      )}

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? 'Creando cuenta…' : 'Crear cuenta'}
      </Button>

      <p className="text-center text-sm text-muted-foreground">
        ¿Ya tienes cuenta?{' '}
        <button
          type="button"
          onClick={onSwitchMode}
          className="font-medium text-primary hover:underline"
        >
          Inicia sesión
        </button>
      </p>
    </form>
  )
}
