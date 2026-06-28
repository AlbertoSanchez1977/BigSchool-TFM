import { z } from 'zod'

export const apiKeySchema = z.object({
  provider: z.string().min(1, 'Selecciona un proveedor'),
  key: z.string().min(10, 'La API key debe tener al menos 10 caracteres'),
})

export type ApiKeyFormValues = z.infer<typeof apiKeySchema>
