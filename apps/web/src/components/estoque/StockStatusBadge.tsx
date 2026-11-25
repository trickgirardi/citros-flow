import { statusInfo, type StockStatus } from "@/components/estoque/stock-data"
import { cn } from "@/lib/utils"

/** Selo do status do estoque: texto + cor (a cor nunca é a única pista). */
export const BADGE_CLASS =
  "inline-flex items-center rounded-pill px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap"

export function StockStatusBadge({ status }: { status: StockStatus }) {
  const info = statusInfo(status)

  return <span className={cn(BADGE_CLASS, info.style)}>{info.label}</span>
}
