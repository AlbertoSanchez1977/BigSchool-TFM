import { z } from 'zod'

export const profileSchema = z
  .object({
    fullName: z.string().min(2, 'El nombre debe tener al menos 2 caracteres'),
    // Contraseña opcional: si está vacía el backend mantiene la actual.
    password: z.string(),
    confirmPassword: z.string(),
  })
  .superRefine((data, ctx) => {
    // Solo validamos reglas de contraseña cuando el usuario quiere cambiarla.
    if (!data.password) return

    if (data.password.length < 8) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'La contraseña debe tener al menos 8 caracteres',
        path: ['password'],
      })
    }

    if (data.password !== data.confirmPassword) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Las contraseñas no coinciden',
        path: ['confirmPassword'],
      })
    }
  })

export type ProfileFormValues = z.infer<typeof profileSchema>
