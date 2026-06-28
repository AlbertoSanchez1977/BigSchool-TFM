'use client'

import { Mail, PartyPopper, MessageSquare } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { useAuth } from '@/hooks/useAuth'
import { useEmails } from '@/hooks/useEmails'

const TYPE_CONFIG = {
  welcome: {
    label: 'Bienvenida',
    icon: PartyPopper,
    badge: 'secondary' as const,
  },
  contact: {
    label: 'Contacto',
    icon: MessageSquare,
    badge: 'outline' as const,
  },
}

function formatDate(iso: string | null) {
  if (!iso) return 'Al registrarte'
  return new Date(iso).toLocaleString('es-ES', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

export default function EmailsPage() {
  const { user } = useAuth()
  // TODO (deuda técnica backend): GET /emails WHERE idUser = @userId OR idUser IS NULL
  const { emails } = useEmails(user)

  return (
    <div className="mx-auto max-w-4xl px-5 py-10 md:px-8">
      <div className="mb-8">
        <h1 className="font-heading text-2xl font-semibold">Registro de emails</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Emails enviados asociados a tu cuenta o al sistema.
        </p>
      </div>

      {emails.length === 0 ? (
        <div
          data-testid="empty-state"
          className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed py-24 text-center"
        >
          <Mail className="size-10 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">No hay emails registrados.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {emails.map((email) => {
            const cfg = TYPE_CONFIG[email.type]
            const Icon = cfg.icon
            return (
              <Card key={email.id}>
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
                        Para: <span className="font-medium">{email.to}</span>
                      </p>
                      <p className="text-sm text-foreground/70 line-clamp-2">{email.preview}</p>
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
