"use client"

import Link from 'next/link'
import { ArrowUpRight } from 'lucide-react'
import { Section } from '@/components/section'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/hooks/useAuth'

export function Cta() {
  const { user, isLoading } = useAuth()

  return (
    <Section id="contacto">
      <div className="flex flex-col items-center gap-6 rounded-2xl border border-border bg-card px-6 py-14 text-center ring-1 ring-foreground/5 md:px-12">
        <h2 className="text-balance font-heading text-3xl font-semibold tracking-tight md:text-4xl">
          Empieza a controlar tus finanzas e inversiones hoy
        </h2>
        <p className="max-w-xl text-pretty leading-relaxed text-muted-foreground">
          Crea tu cuenta gratuita, conecta tus gastos y carteras, y deja que el asistente de IA te acompañe en tu camino
          hacia el value investing.
        </p>
          { !isLoading && !user && (
          <div className="flex flex-col gap-3 sm:flex-row">
            {/* render={<Link />} hace que Button se renderice como <a> manteniendo todos sus estilos */}
            {/*
              nativeButton={false}: le decimos explícitamente a Base UI que sabemos que
              estamos renderizando un <a> (Link de navegación), no un <button>.
              Semánticamente es correcto: <a> = navegar a otro sitio, <button> = acción.
            */}
              <Button size="lg" className="gap-1.5" render={<Link href="/register" />} nativeButton={false}>
                Registrarse
                <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
              </Button>
              <Button size="lg" variant="outline" render={<Link href="/login" />} nativeButton={false}>
                Iniciar sesión
              </Button>
            
          </div>
          )}
      </div>
    </Section>
  )
}
