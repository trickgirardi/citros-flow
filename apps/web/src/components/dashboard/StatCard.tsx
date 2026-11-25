import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

type StatCardProps = {
  /** Linha de apoio sob o número (ex.: "Saldo anterior R$ 1.000,00"). */
  detail?: ReactNode
  label: string
  /** Cor do número: azul (positivo) ou vermelho (negativo). */
  tone?: "negative" | "neutral" | "positive"
  /** O número já formatado ("R$ 1.234,56", "12"). */
  value: string
}

/** Cartão de indicador (modelo do IGB): rótulo, número em destaque e uma linha de apoio. */
export function StatCard({
  detail,
  label,
  tone = "neutral",
  value,
}: StatCardProps) {
  return (
    <div className="flex min-w-0 flex-col gap-1 rounded-card border bg-card p-4 shadow-card">
      <p className="text-sm font-semibold text-muted-foreground">{label}</p>
      <p
        className={cn(
          "text-2xl leading-8 font-bold whitespace-nowrap tabular-nums",
          tone === "positive" && "text-accent-ink",
          tone === "negative" && "text-destructive"
        )}
      >
        {value}
      </p>
      {detail ? (
        <p className="text-sm text-muted-foreground">{detail}</p>
      ) : null}
    </div>
  )
}
