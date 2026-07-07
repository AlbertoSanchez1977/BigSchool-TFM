'use client'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { notificationService } from '@/services/notificationService'
import type { CreateContactDto } from '@/types/notifications'

const CONTACTS_KEY = ['contacts'] as const

// Lectura: GET /contacts (listado de mensajes recibidos, vista de administración).
export function useContacts() {
  const q = useQuery({ queryKey: CONTACTS_KEY, queryFn: () => notificationService.listContacts() })
  return { contacts: q.data ?? [], isLoading: q.isLoading, isError: q.isError }
}

// Escritura: POST /contacts (público, lo usa el formulario de la landing).
// Al crear un contacto invalidamos la lista para que la vista de administración se refresque.
export function useCreateContact() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateContactDto) => notificationService.createContact(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: CONTACTS_KEY }),
  })
}
