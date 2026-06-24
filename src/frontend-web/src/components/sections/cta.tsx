import { ArrowUpRight } from "lucide-react"
import { Section } from "@/components/section"
import { Button } from "@/components/ui/button"

export function Cta() {
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
        <div className="flex flex-col gap-3 sm:flex-row">
          <Button size="lg" className="gap-1.5">
            Registrarse
            <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
          </Button>
          <Button size="lg" variant="outline">
            Iniciar sesión
          </Button>
        </div>
      </div>
    </Section>
  )
}
