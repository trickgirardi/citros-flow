import Link from "next/link"

import { DashboardSection } from "@/components/dashboard/DashboardSection"
import { StatCard } from "@/components/dashboard/StatCard"
import { StockStatusBadge } from "@/components/estoque/StockStatusBadge"
import { STOCK_STATUSES } from "@/components/estoque/stock-data"
import type { components } from "@/lib/api/schema"

type Stock = components["schemas"]["DashboardStock"]

/** Contagem por status e os itens que pedem ação (comprar, com urgência primeiro). */
export function StockSummary({
  boardId,
  stock,
}: {
  boardId: string
  stock: Stock
}) {
  const href = `/board/${boardId}/estoque`
  const more = stock.attentionTotal - stock.attention.length

  return (
    <DashboardSection href={href} linkLabel="Ver estoque" title="Estoque">
      <div className="grid grid-cols-2 gap-2 @lg:grid-cols-4">
        {STOCK_STATUSES.map((status) => (
          <StatCard
            key={status.status}
            label={status.label}
            // Urgente em aberto chama atenção (texto + cor: o rótulo já diz "urgência").
            tone={
              status.status === "comprarComUrgencia" && stock[status.status] > 0
                ? "negative"
                : "neutral"
            }
            value={String(stock[status.status])}
          />
        ))}
      </div>

      <div className="grid gap-2">
        <h3 className="text-base font-bold">Precisa de ação</h3>
        {stock.attention.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Nada para comprar agora.
          </p>
        ) : (
          <ul className="grid gap-1.5">
            {stock.attention.map((item) => (
              <li key={item.id}>
                <Link
                  href={href}
                  className="flex items-center justify-between gap-3 rounded-field border px-3 py-2 hover:bg-accent"
                >
                  <span className="min-w-0">
                    <span className="block font-semibold [overflow-wrap:anywhere]">
                      {item.name}
                    </span>
                    {item.assignee ? (
                      <span className="block text-sm text-muted-foreground">
                        {item.assignee.name}
                      </span>
                    ) : null}
                  </span>
                  <StockStatusBadge status={item.status} />
                </Link>
              </li>
            ))}
          </ul>
        )}
        {more > 0 ? (
          <p className="text-sm text-muted-foreground">e mais {more}.</p>
        ) : null}
      </div>
    </DashboardSection>
  )
}
