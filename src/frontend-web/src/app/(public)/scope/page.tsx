import Link from 'next/link'
import { CheckCircle2, Clock, ExternalLink } from 'lucide-react'
import { NavbarPublic } from '@/components/navbar-public'
import { Footer } from '@/components/footer'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'

// ── Datos de contenido ────────────────────────────────────────────────────────

const mvpFeatures = [
  {
    area: 'Infraestructura',
    items: [
      'Monorepo Next.js 15 + .NET 8 + Docker Compose (MySQL + Qdrant)',
      'CI por ramas con PRs a develop + gate de revisión humana',
      'TDD: Vitest + RTL (unit) · Playwright (E2E) · xUnit (backend)',
      'Autenticación JWT con DPAPI (userId cifrado en token, refresh)',
    ],
  },
  {
    area: 'Landing pública',
    items: [
      'Hero con propuesta de valor y CTAs de registro/login',
      'Secciones de producto: Gastos, Inversiones, AI Scanner (demos visuales)',
      'Formulario de contacto integrado (localStorage)',
      'Footer con navegación y aviso legal',
    ],
  },
  {
    area: 'Finanzas personales',
    items: [
      'Dashboard con resumen mensual y gráfica de evolución (Recharts)',
      'CRUD completo de Gastos/Ingresos con filtros y paginación',
      'Categorías dinámicas (globales + personalizadas por usuario)',
      'Soporte multimoneda (backend: EUR, USD, GBP, CHF, JPY con tipos de cambio reales)',
    ],
  },
  {
    area: 'Inversiones',
    items: [
      'Gestión de carteras y holdings (compra con snapshot de tipo)',
      'Venta FIFO cross-lot con RealizedPnL consolidado',
      'Performance: plusvalía realizada + no realizada en moneda base',
      'Catálogo de empresas con valoraciones históricas',
    ],
  },
  {
    area: 'AI Scanner (demo)',
    items: [
      'UI de chat con selector de LLM (Claude / GPT / Gemini)',
      'Panel RAG de documentos (estado local, pendiente de backend)',
      'Gestión de API keys por proveedor (localStorage)',
      'Feature-flag NEXT_PUBLIC_AI_SCANNER_ENABLED para activar la demo',
    ],
  },
  {
    area: 'Cuenta y perfil',
    items: [
      'Perfil de usuario: info de cuenta + edición de nombre y contraseña',
      'Listado de emails enviados (bienvenida + contacto) — demo local',
      'Listado de contactos recibidos — demo local (localStorage)',
      'ProfileDropdown como hub de páginas privadas secundarias',
    ],
  },
]

const futureWork = [
  {
    area: 'IA / RAG',
    priority: 'high',
    items: [
      'Backend de AI Scanner: POST /ai/chat con streaming + historial',
      'Upload de documentos a Qdrant (embeddings, chunking, búsqueda semántica)',
      'Integración con Azure OpenAI / LLM externo (bloqueado por suscripción)',
      'MCP Server en Python: herramientas de screener y revisión de cartera',
    ],
  },
  {
    area: 'Backend pendiente',
    priority: 'high',
    items: [
      'GET / PUT /users/me (baseCurrency, lastLoginDate — contrato documentado en frontend)',
      'POST /contact + tabla de contactos en BD (actualmente localStorage)',
      'GET /emails: tabla EmailLog con idUser nullable (spec documentada en frontend)',
      'Envío real de emails de bienvenida (SendGrid / SES)',
    ],
  },
  {
    area: 'Producto',
    priority: 'medium',
    items: [
      'App Mobile React Native Expo (solo lectura, consume el mismo backend)',
      'Exportación a CSV / Excel de transacciones y cartera',
      'Alertas y notificaciones (precio objetivo, resumen semanal)',
      'Modo multi-divisa en UI (el backend ya convierte; falta el selector en frontend)',
    ],
  },
  {
    area: 'Plataforma',
    priority: 'low',
    items: [
      'Rol admin para gestión de contactos desde backoffice',
      'Kubernetes (manifiestos ya esquematizados en infra/k8s)',
      'Rate limiting y auditoría de accesos',
      'OAuth2 / inicio de sesión con Google',
    ],
  },
]

const priorityLabel: Record<string, string> = {
  high: 'Prioridad alta',
  medium: 'Prioridad media',
  low: 'En roadmap',
}

const priorityVariant: Record<string, 'default' | 'secondary' | 'outline'> = {
  high: 'default',
  medium: 'secondary',
  low: 'outline',
}

// ── Componentes ───────────────────────────────────────────────────────────────

function FeatureList({ items }: { items: string[] }) {
  return (
    <ul className="mt-2 space-y-1.5">
      {items.map((item) => (
        <li key={item} className="flex items-start gap-2 text-sm text-muted-foreground">
          <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-positive" />
          {item}
        </li>
      ))}
    </ul>
  )
}

function FutureList({ items }: { items: string[] }) {
  return (
    <ul className="mt-2 space-y-1.5">
      {items.map((item) => (
        <li key={item} className="flex items-start gap-2 text-sm text-muted-foreground">
          <Clock className="mt-0.5 size-4 shrink-0 text-warning" />
          {item}
        </li>
      ))}
    </ul>
  )
}

// ── Página ────────────────────────────────────────────────────────────────────

export default function ScopePage() {
  return (
    <div className="flex min-h-screen flex-col bg-background font-sans">
      <NavbarPublic />

      <main className="flex-1">
        {/* ── Cabecera ──────────────────────────────────────────────────────── */}
        <section className="border-b border-border bg-muted/30 py-16">
          <div className="mx-auto max-w-4xl px-5 text-center md:px-8">
            <Badge variant="secondary" className="mb-4">TFM · Curso 2025-2026</Badge>
            <h1 className="font-heading text-4xl font-bold tracking-tight md:text-5xl">
              Alcance y trabajos futuros
            </h1>
            <p className="mx-auto mt-4 max-w-2xl text-lg text-muted-foreground">
              BigSchool es un proyecto educativo de finanzas personales e inversiones con IA.
              Esta página documenta qué está implementado en el MVP y qué queda en el roadmap.
            </p>
            <p className="mt-3 text-sm text-muted-foreground">
              Backend .NET 8 + Frontend Next.js 15 + Demo de AI Scanner ·{' '}
              <Link
                href="https://github.com/AlbertoSanchez1977/BigSchool-TFM"
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1 underline underline-offset-2 hover:text-foreground"
              >
                Ver código fuente <ExternalLink className="size-3" />
              </Link>
            </p>
          </div>
        </section>

        {/* ── MVP implementado ──────────────────────────────────────────────── */}
        <section className="py-14">
          <div className="mx-auto max-w-4xl px-5 md:px-8">
            <div className="mb-8 flex items-center gap-3">
              <CheckCircle2 className="size-6 text-positive" />
              <h2 className="font-heading text-2xl font-semibold">Implementado en el MVP</h2>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              {mvpFeatures.map((section) => (
                <Card key={section.area}>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-base">{section.area}</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <FeatureList items={section.items} />
                  </CardContent>
                </Card>
              ))}
            </div>
          </div>
        </section>

        <Separator className="mx-auto max-w-4xl" />

        {/* ── Trabajos futuros ──────────────────────────────────────────────── */}
        <section className="py-14">
          <div className="mx-auto max-w-4xl px-5 md:px-8">
            <div className="mb-8 flex items-center gap-3">
              <Clock className="size-6 text-warning" />
              <h2 className="font-heading text-2xl font-semibold">Trabajos futuros</h2>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              {futureWork.map((section) => (
                <Card key={section.area}>
                  <CardHeader className="pb-2">
                    <div className="flex items-center justify-between gap-2">
                      <CardTitle className="text-base">{section.area}</CardTitle>
                      <Badge variant={priorityVariant[section.priority]} className="shrink-0 text-xs">
                        {priorityLabel[section.priority]}
                      </Badge>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <FutureList items={section.items} />
                  </CardContent>
                </Card>
              ))}
            </div>
          </div>
        </section>

        {/* ── Nota disclaimer ───────────────────────────────────────────────── */}
        <section className="border-t border-border bg-muted/30 py-10">
          <div className="mx-auto max-w-3xl px-5 text-center md:px-8">
            <p className="text-sm text-muted-foreground">
              Los datos mostrados en la aplicación son ilustrativos y no constituyen asesoramiento
              financiero. BigSchool es un proyecto educativo desarrollado como Trabajo Fin de Máster.
            </p>
          </div>
        </section>
      </main>

      <Footer />
    </div>
  )
}
