import type { components } from "@/lib/api/schema"
import { toCents } from "@/lib/money"

export type StockItem = components["schemas"]["StockItemDto"]
export type StockStatus = components["schemas"]["StockStatus"]
export type StockPerson = components["schemas"]["TaskAssigneeDto"]

type StockStatusInfo = {
  label: string
  status: StockStatus
  /** 0 = o mais urgente: é a ordem padrão da tabela. */
  urgency: number
  /** Selo com texto e cor (a cor nunca é a única pista). */
  style: string
}

/** Na ordem dos filtros da tela. */
export const STOCK_STATUSES: readonly StockStatusInfo[] = [
  {
    label: "Em estoque",
    status: "emEstoque",
    style: "bg-accent-ink/10 text-accent-ink",
    urgency: 3,
  },
  {
    label: "A caminho",
    status: "aCaminho",
    style: "bg-primary/30 text-foreground",
    urgency: 2,
  },
  {
    label: "Comprar",
    status: "comprar",
    style: "bg-muted text-foreground",
    urgency: 1,
  },
  {
    label: "Comprar com urgência",
    status: "comprarComUrgencia",
    style: "bg-destructive/10 text-destructive",
    urgency: 0,
  },
]

export function statusInfo(status: StockStatus) {
  return STOCK_STATUSES.find((item) => item.status === status)!
}

export const STOCK_LIMITS = { details: 2000, name: 120 } as const

export type SortKey = "assignee" | "name" | "price" | "status"
/** null = ordem padrão: urgência primeiro, depois nome. */
export type StockSort = { direction: "asc" | "desc"; key: SortKey } | null

/** Minúsculas e sem acento, para a busca não depender de como a pessoa digita. */
export function normalizeText(text: string) {
  return text
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase()
}

export function filterItems(
  items: readonly StockItem[],
  query: string,
  status: StockStatus | "all"
) {
  const needle = normalizeText(query.trim())

  return items.filter(
    (item) =>
      (status === "all" || item.status === status) &&
      (needle === "" || normalizeText(item.name).includes(needle))
  )
}

const byName = (first: StockItem, second: StockItem) =>
  first.name.localeCompare(second.name, "pt-BR")

/**
 * Ordena sem alterar a lista. Sem `sort`: urgência e nome. Com `sort`: pela coluna (status vale
 * pela urgência) e, em empate, por nome. Itens sem responsável ou sem preço ficam por último nos
 * dois sentidos.
 */
export function sortItems(items: readonly StockItem[], sort: StockSort) {
  const sorted = [...items]

  if (!sort) {
    return sorted.sort(
      (first, second) =>
        statusInfo(first.status).urgency - statusInfo(second.status).urgency ||
        byName(first, second)
    )
  }

  const sign = sort.direction === "asc" ? 1 : -1
  const empty = (item: StockItem) =>
    (sort.key === "assignee" && item.assignee === null) ||
    (sort.key === "price" && item.price === null)

  return sorted.sort((first, second) => {
    if (empty(first) !== empty(second)) {
      return empty(first) ? 1 : -1
    }

    let result = 0

    if (sort.key === "name") {
      result = byName(first, second)
    } else if (sort.key === "status") {
      result =
        statusInfo(first.status).urgency - statusInfo(second.status).urgency
    } else if (sort.key === "assignee") {
      result = (first.assignee?.name ?? "").localeCompare(
        second.assignee?.name ?? "",
        "pt-BR"
      )
    } else {
      result = toCents(first.price ?? "0") - toCents(second.price ?? "0")
    }

    return result * sign || byName(first, second)
  })
}

/** Clicar no cabeçalho: crescente, depois decrescente, depois volta ao padrão. */
export function nextSort(current: StockSort, key: SortKey): StockSort {
  if (current?.key !== key) {
    return { direction: "asc", key }
  }

  return current.direction === "asc" ? { direction: "desc", key } : null
}

/** "1234.56" -> "1234,56": o formato que a pessoa digita no campo de preço. */
export function priceToInput(price: string | null) {
  return price === null ? "" : price.replace(".", ",")
}
