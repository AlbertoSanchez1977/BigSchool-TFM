import { Filter, FileSearch, ClipboardCheck } from "lucide-react"
import { Section, SectionHeader } from "@/components/section"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"

const flows = [
  {
    icon: Filter,
    title: "Screener",
    description:
      "Filtra empresas por métricas fundamentales y descarta rápidamente las que no encajan con tu estrategia de value investing.",
  },
  {
    icon: FileSearch,
    title: "Criterios",
    description:
      "Análisis a fondo de una empresa: el asistente revisa criterios de calidad, deuda y valoración para darte una visión completa.",
  },
  {
    icon: ClipboardCheck,
    title: "Revisión de cartera",
    description:
      "Evalúa tus posiciones actuales, detecta concentraciones de riesgo y recibe sugerencias sobre tu asignación.",
  },
]

export function AiScanner() {
  return (
    <Section id="ai-scanner" className="border-b border-border">
      <SectionHeader
        eyebrow="AI Scanner"
        title="Tu copiloto para el análisis de inversiones"
        description="Tres flujos de inteligencia artificial pensados para acompañarte en cada fase de tu proceso de inversión."
      />
      <span className="mt-4 inline-flex w-fit items-center rounded-full border border-primary/30 bg-accent px-3 py-1 text-xs font-medium text-accent-foreground">
        Próximamente
      </span>

      <div className="mt-8 grid gap-5 md:grid-cols-3">
        {flows.map((flow) => (
          <Card key={flow.title} className="h-full">
            <CardHeader>
              <span className="flex h-11 w-11 items-center justify-center rounded-lg bg-accent text-accent-foreground">
                <flow.icon className="h-5 w-5" aria-hidden="true" />
              </span>
              <CardTitle className="mt-3">{flow.title}</CardTitle>
              <CardDescription className="leading-relaxed">{flow.description}</CardDescription>
            </CardHeader>
            <CardContent>
              <span className="text-xs font-medium text-muted-foreground">En desarrollo</span>
            </CardContent>
          </Card>
        ))}
      </div>
    </Section>
  )
}
