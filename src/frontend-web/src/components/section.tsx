import type React from "react"
import { cn } from "@/lib/utils"

interface SectionProps extends React.HTMLAttributes<HTMLElement> {
  id?: string
  children: React.ReactNode
  className?: string
  containerClassName?: string
}

export function Section({ id, children, className, containerClassName, ...props }: SectionProps) {
  return (
    <section id={id} className={cn("w-full py-16 md:py-24", className)} {...props}>
      <div className={cn("mx-auto w-full max-w-6xl px-5 md:px-8", containerClassName)}>{children}</div>
    </section>
  )
}

interface SectionHeaderProps {
  eyebrow?: string
  title: string
  description?: string
  align?: "left" | "center"
  className?: string
}

export function SectionHeader({ eyebrow, title, description, align = "left", className }: SectionHeaderProps) {
  return (
    <div
      className={cn(
        "flex flex-col gap-3",
        align === "center" && "items-center text-center mx-auto max-w-2xl",
        className,
      )}
    >
      {eyebrow && (
        <span className="inline-flex w-fit items-center rounded-full bg-accent px-3 py-1 text-xs font-medium uppercase tracking-wide text-accent-foreground">
          {eyebrow}
        </span>
      )}
      <h2 className="text-balance font-heading text-3xl font-semibold tracking-tight md:text-4xl">{title}</h2>
      {description && <p className="max-w-2xl text-pretty leading-relaxed text-muted-foreground">{description}</p>}
    </div>
  )
}
