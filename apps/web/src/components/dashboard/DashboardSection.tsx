import Link from "next/link"
import { ArrowRight } from "lucide-react"
import type { ReactNode } from "react"

type DashboardSectionProps = {
  children: ReactNode
  /** Para onde vai o "Ver tudo" do bloco. */
  href: string
  linkLabel: string
  title: string
}

/** Bloco do dashboard: título, link para o módulo e o conteúdo. */
export function DashboardSection({
  children,
  href,
  linkLabel,
  title,
}: DashboardSectionProps) {
  return (
    <section className="grid content-start gap-3 rounded-panel border bg-card p-4 shadow-card sm:p-5">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1">
        <h2 className="font-sans text-lg font-bold">{title}</h2>
        <Link
          href={href}
          className="inline-flex items-center gap-1 rounded-lg text-sm font-semibold text-accent-ink hover:underline"
        >
          {linkLabel}
          <ArrowRight className="size-4" />
        </Link>
      </div>
      {children}
    </section>
  )
}
