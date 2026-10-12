import type { components } from "@/lib/api/schema"

// Tipos vêm do contrato da API (pnpm gen:api). Dinheiro é string decimal ("1234.56"); totais,
// agrupamentos e saldos já chegam calculados. Para exibir, use formatMoney de @/lib/money.
export type Transaction = components["schemas"]["TransactionDto"]
export type TransactionType = Transaction["type"]
export type TransactionGroup = components["schemas"]["CategoryGroupDto"]
export type FinancialReserve = components["schemas"]["ReserveDto"]

const dateFormatter = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "2-digit",
});

export function formatTransactionDate(value: string) {
  return dateFormatter.format(new Date(`${value}T00:00:00`));
}
