'use client'
import { useQuery } from '@tanstack/react-query'
import { notificationService } from '@/services/notificationService'

// GET /emails — el backend ya filtra por el usuario autenticado (emails propios +
// emails de sistema con idUser NULL, p.ej. acuses de contacto). Ya no recibe `user`
// como parámetro: la identidad viaja en el JWT, no hace falta pasarla a mano.
export function useEmails() {
  const q = useQuery({ queryKey: ['emails'], queryFn: () => notificationService.listEmails() })
  return { emails: q.data ?? [], isLoading: q.isLoading, isError: q.isError }
}
