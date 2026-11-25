"use client"

import { ChevronDown } from "lucide-react"

import {
  STOCK_STATUSES,
  statusInfo,
  type StockItem,
  type StockStatus,
} from "@/components/estoque/stock-data"
import { cn } from "@/lib/utils"

const BADGE =
  "inline-flex items-center rounded-pill px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap"

/** O status como selo (texto + cor), ou como seletor na própria linha para quem gerencia. */
export function StockStatusCell({
  canManage,
  item,
  onChange,
}: {
  canManage: boolean
  item: StockItem
  onChange: (status: StockStatus) => void
}) {
  const info = statusInfo(item.status)

  if (!canManage) {
    return <span className={cn(BADGE, info.style)}>{info.label}</span>
  }

  return (
    // Trocar o status não abre o item: a linha inteira é clicável.
    <div
      className="relative inline-block"
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => event.stopPropagation()}
    >
      <select
        aria-label={`Status de ${item.name}`}
        value={item.status}
        onChange={(event) => onChange(event.target.value as StockStatus)}
        className={cn(
          BADGE,
          info.style,
          "cursor-pointer appearance-none py-1 pr-7 pl-2.5"
        )}
      >
        {STOCK_STATUSES.map((status) => (
          <option key={status.status} value={status.status}>
            {status.label}
          </option>
        ))}
      </select>
      <ChevronDown className="pointer-events-none absolute top-1/2 right-2 size-3.5 -translate-y-1/2" />
    </div>
  )
}
