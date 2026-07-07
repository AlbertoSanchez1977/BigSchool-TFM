'use client'

import { Mail, PartyPopper, MessageSquare } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useEmails } from '@/hooks/useEmails'
import { EMAIL_TYPE } from '@/types/notifications'

// EmailType (backend, Domain/Notifications/Enums/EmailType.cs): 1 = Welcome, 2 = Contact.
// El DTO de listado (Dapper) lo devuelve como número crudo, no como string.
const TYPE_CONFIG: Record<number, { label: string; icon: typeof PartyPopper; badge: 'secondary' | 'outline' }> = {
  [EMAIL_TYPE.Welcome]: { label: 'Bienvenida', icon: PartyPopper, badge: 'secondary' },
  [EMAIL_TYPE.Contact]: { label: 'Contacto', icon: MessageSquare, badge: 'outline' },
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString('es-ES', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

export default function EmailsPage() {
  const { emails, isLoading, isError } = useEmails()

  return (
    <div className="mx-auto max-w-4xl px-5 py-10 md:px-8">
      <div className="mb-8">
        <h1 className="font-heading text-2xl font-semibold">Registro de emails</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Emails enviados asociados a tu cuenta o al sistema.
        </p>
      </div>

      {isLoading && (
        <div className="space-y-3" data-testid="loading-state">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-[76px] rounded-lg" />
          ))}
        </div>
      )}

      {!isLoading && isError && (
        <div
          data-testid="error-state"
          className="rounded-2xl border border-border bg-card py-10 text-center text-sm text-destructive"
        >
          No se pudieron cargar los emails. Inténtalo de nuevo más tarde.
        </div>
      )}

      {!isLoading && !isError && emails.length === 0 && (
        <div
          data-testid="empty-state"
          className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed py-24 text-center"
        >
          <Mail className="size-10 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">No hay emails registrados.</p>
        </div>
      )}

      {!isLoading && !isError && emails.length > 0 && (
        <div className="space-y-3">
          {emails.map((email) => {
            const cfg = TYPE_CONFIG[email.type] ?? TYPE_CONFIG[EMAIL_TYPE.Contact]
            const Icon = cfg.icon
            return (
              <Card key={email.idEmailLog}>
                <CardContent className="pt-5">
                  <div className="flex items-start gap-4">
                    <div className="flex size-9 shrink-0 items-center justify-center rounded-full bg-muted">
                      <Icon className="size-4 text-muted-foreground" />
                    </div>

                    <div className="flex min-w-0 flex-1 flex-col gap-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge variant={cfg.badge}>{cfg.label}</Badge>
                        <span className="text-sm font-medium truncate">{email.subject}</span>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        Para: <span className="font-medium">{email.recipient}</span>
                      </p>
                    </div>

                    <time className="shrink-0 text-xs text-muted-foreground whitespace-nowrap">
                      {formatDate(email.sentAt)}
                    </time>
                  </div>
                </CardContent>
              </Card>
            )
          })}
        </div>
      )}
    </div>
  )
}
