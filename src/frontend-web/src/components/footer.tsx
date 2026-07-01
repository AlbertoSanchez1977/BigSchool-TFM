'use client'

import Link from 'next/link'
import { LineChart } from "lucide-react"
import { useAuth } from "@/hooks/useAuth"

const footerColumns = [
  {
    title: "Producto",
    links: [
      { label: "Gastos e ingresos", href: "/#gastos" },
      { label: "Inversiones", href: "/#inversiones" },
      { label: "AI Scanner", href: "/#ai-scanner" },
    ],
  },
  {
    title: "Recursos",
    links: [
      { label: "Contacto", href: "/#contacto" },
      { label: "Alcance y trabajos futuros", href: "/scope" },
      { label: "Iniciar sesión", href: "#" },
    ],
  },
]

export function Footer() {
  const { user, isLoading } = useAuth()

  return (
    <footer className="w-full border-t border-border bg-muted/40">
      <div className="mx-auto w-full max-w-6xl px-5 py-12 md:px-8">
        <div className="flex flex-col gap-10 md:flex-row md:justify-between">
          <div className="max-w-sm">
            <div className="flex items-center gap-2">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                <LineChart className="h-5 w-5" aria-hidden="true" />
              </span>
              <span className="font-heading text-lg font-semibold tracking-tight">BigSchool</span>
            </div>
            <p className="mt-4 text-pretty text-sm leading-relaxed text-muted-foreground">
              Finanzas personales e inversiones con enfoque de value investing, potenciadas por un asistente de IA.
            </p>
          </div>

          <div className="grid grid-cols-2 gap-10 sm:gap-16">
            {footerColumns.map((col) => (
              <div key={col.title}>
                <h3 className="text-sm font-semibold text-foreground">{col.title}</h3>
                <ul className="mt-4 flex flex-col gap-3">
                  {col.links
                    .filter((link) => !(link.label === "Iniciar sesión" && !isLoading && user))
                    .map((link) => (
                      <li key={link.label}>
                        <Link
                          href={link.href}
                          className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                        >
                          {link.label}
                        </Link>
                      </li>
                    ))}
                </ul>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-10 flex flex-col gap-2 border-t border-border pt-6 text-xs text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
          <p>© {new Date().getFullYear()} BigSchool. Proyecto educativo de finanzas e inversión.</p>
          <p>Los datos mostrados son ilustrativos y no constituyen asesoramiento financiero.</p>
        </div>
      </div>
    </footer>
  )
}
