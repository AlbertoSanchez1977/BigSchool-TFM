'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { CheckCircle2, Send } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { contactSchema, type ContactFormValues } from '@/lib/schemas/contact'
import { saveContact } from '@/hooks/useContacts'

export function ContactForm() {
  const [sent, setSent] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ContactFormValues>({
    resolver: zodResolver(contactSchema),
  })

  function onSubmit(values: ContactFormValues) {
    saveContact(values)
    setSent(true)
    reset()
  }

  if (sent) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-border bg-card px-6 py-12 text-center">
        <CheckCircle2 className="size-10 text-positive" />
        <h3 className="font-heading text-xl font-semibold">¡Mensaje enviado!</h3>
        <p className="text-sm text-muted-foreground">
          Gracias por escribirnos. Te responderemos a la mayor brevedad posible.
        </p>
        <Button variant="outline" size="sm" onClick={() => setSent(false)}>
          Enviar otro mensaje
        </Button>
      </div>
    )
  }

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      className="rounded-2xl border border-border bg-card px-6 py-10 space-y-5"
    >
      <div>
        <h3 className="font-heading text-xl font-semibold">¿Alguna pregunta?</h3>
        <p className="mt-1 text-sm text-muted-foreground">
          Escríbenos y te respondemos en breve.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="contact-name">Nombre</Label>
          <Input
            id="contact-name"
            placeholder="Tu nombre"
            aria-invalid={!!errors.fullName}
            {...register('fullName')}
          />
          {errors.fullName && (
            <p className="text-xs text-destructive">{errors.fullName.message}</p>
          )}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="contact-email">Tu email</Label>
          <Input
            id="contact-email"
            type="email"
            placeholder="tu@email.com"
            aria-invalid={!!errors.email}
            {...register('email')}
          />
          {errors.email && (
            <p className="text-xs text-destructive">{errors.email.message}</p>
          )}
        </div>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="contact-message">Mensaje</Label>
        <Textarea
          id="contact-message"
          placeholder="Cuéntanos en qué podemos ayudarte…"
          rows={4}
          aria-invalid={!!errors.message}
          {...register('message')}
        />
        {errors.message && (
          <p className="text-xs text-destructive">{errors.message.message}</p>
        )}
      </div>

      <Button
        type="submit"
        size="lg"
        className="w-full gap-2 shadow-sm transition-transform active:scale-95"
        disabled={isSubmitting}
      >
        <Send className="size-4" />
        Enviar mensaje
      </Button>
    </form>
  )
}
