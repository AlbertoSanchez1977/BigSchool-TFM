'use client'
import { useState } from 'react'

export interface ContactSubmission {
  id: string
  fullName: string
  email: string
  message: string
  submittedAt: string
}

const LS_KEY = 'contact_submissions'

export function loadContacts(): ContactSubmission[] {
  if (typeof window === 'undefined') return []
  try {
    const raw = localStorage.getItem(LS_KEY)
    return raw ? (JSON.parse(raw) as ContactSubmission[]) : []
  } catch {
    return []
  }
}

export function saveContact(submission: Omit<ContactSubmission, 'id' | 'submittedAt'>): ContactSubmission {
  // TODO (deuda técnica backend): POST /contact { fullName, email, message }
  // En producción este endpoint guarda en BD y dispara un email simulado.
  const entry: ContactSubmission = {
    ...submission,
    id: crypto.randomUUID(),
    submittedAt: new Date().toISOString(),
  }
  const existing = loadContacts()
  localStorage.setItem(LS_KEY, JSON.stringify([entry, ...existing]))
  return entry
}

export function useContacts() {
  const [contacts] = useState<ContactSubmission[]>(loadContacts)
  return { contacts }
}
