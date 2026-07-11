'use client'

import { Mail, MessageSquare } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useContacts } from '@/hooks/useContacts'
import { formatDateTimeUtc } from '@/lib/dates'

export default function ContactsPage() {
  const { contacts, isLoading, isError } = useContacts()

  return (
    <div className="mx-auto max-w-4xl px-5 py-10 md:px-8">
      <div className="mb-8 flex items-center justify-between gap-4">
        <div>
          <h1 className="font-heading text-2xl font-semibold">Mensajes de contacto</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Envíos recibidos a través del formulario de la landing.
          </p>
        </div>
        <Button variant="outline" size="sm" render={<Link href="/#contacto" />} nativeButton={false}>
          Nuevo mensaje
        </Button>
      </div>

      {isLoading && (
        <div className="space-y-4" data-testid="loading-state">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-[88px] rounded-2xl" />
          ))}
        </div>
      )}

      {!isLoading && isError && (
        <div
          data-testid="error-state"
          className="rounded-2xl border border-border bg-card py-10 text-center text-sm text-destructive"
        >
          No se pudieron cargar los mensajes. Inténtalo de nuevo más tarde.
        </div>
      )}

      {!isLoading && !isError && contacts.length === 0 && (
        <div
          data-testid="empty-state"
          className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed py-24 text-center"
        >
          <MessageSquare className="size-10 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            Aún no hay mensajes. El formulario está en la{' '}
            <Link href="/#contacto" className="underline underline-offset-2 hover:text-foreground">
              página principal
            </Link>
            .
          </p>
        </div>
      )}

      {!isLoading && !isError && contacts.length > 0 && (
        <div className="space-y-4">
          {contacts.map((c) => (
            <Card key={c.idContact}>
              <CardContent className="pt-5">
                <div className="flex items-start justify-between gap-4">
                  <div className="flex min-w-0 flex-col gap-1">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-sm">{c.fullName}</span>
                      <span className="flex items-center gap-1 text-xs text-muted-foreground">
                        <Mail className="size-3" />
                        {c.email}
                      </span>
                    </div>
                    <p className="text-sm text-foreground/80 line-clamp-3">{c.message}</p>
                  </div>
                  <time className="shrink-0 text-xs text-muted-foreground whitespace-nowrap">
                    {formatDateTimeUtc(c.createdAt)}
                  </time>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
