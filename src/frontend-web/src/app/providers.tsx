'use client'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { Toaster } from '@/components/ui/sonner'
import { AuthProvider } from '@/lib/auth/AuthProvider'

export function Providers({ children }: { children: React.ReactNode }) {
  // useState garantiza que cada usuario/request tiene su propio QueryClient (análogo a un
  // scope de DI por request en ASP.NET Core). Si creáramos el QueryClient fuera del
  // componente, se compartiría entre todas las requests en el servidor.
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60 * 1000, // 1 min antes de refetch automático
            retry: 1,
          },
        },
      }),
  )

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        {children}
        <Toaster position="bottom-right" richColors />
      </AuthProvider>
    </QueryClientProvider>
  )
}
