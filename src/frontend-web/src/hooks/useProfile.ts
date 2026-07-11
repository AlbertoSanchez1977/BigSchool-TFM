'use client'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { userService } from '@/services/userService'
import type { UpdateUserDto } from '@/types/users'

const ME_KEY = ['users', 'me'] as const

// GET /users/me
export function useProfile() {
  return useQuery({ queryKey: ME_KEY, queryFn: () => userService.me() })
}

// PUT /users/me — al éxito, escribe la respuesta directamente en la caché de useProfile
// (setQueryData) en vez de invalidar: nos ahorramos un roundtrip porque el backend ya
// devuelve el UserProfile actualizado completo.
export function useUpdateProfile() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: UpdateUserDto) => userService.update(data),
    onSuccess: (updated) => qc.setQueryData(ME_KEY, updated),
  })
}
