import { z } from 'zod'

export const contactSchema = z.object({
  fullName: z.string().min(2, 'El nombre debe tener al menos 2 caracteres'),
  email: z.string().email('Email inválido'),
  message: z
    .string()
    .min(10, 'El mensaje debe tener al menos 10 caracteres')
    .max(1000, 'Máximo 1000 caracteres'),
})

export type ContactFormValues = z.infer<typeof contactSchema>
