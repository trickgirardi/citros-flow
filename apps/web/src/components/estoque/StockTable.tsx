"use client"

import { useMemo, useOptimistic, useState, useTransition } from "react"
import { ArrowDown, ArrowUp, ArrowUpDown, Plus, Search } from "lucide-react"

import { setStockItemStatus } from "@/app/(app)/board/[boardId]/estoque/actions"
import { PageContent } from "@/components/layout/page-content"
import { PageHeader } from "@/components/layout/page-header"
import {
  StockFormModal,
  type StockFormTarget,
} from "@/components/estoque/StockFormModal"
import { StockStatusCell } from "@/components/estoque/StockStatusCell"
import {
  filterItems,
  nextSort,
  sortItems,
  STOCK_STATUSES,
  type SortKey,
  type StockItem,
  type StockPerson,
  type StockSort,
  type StockStatus,
} from "@/components/estoque/stock-data"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { formatMoney } from "@/lib/money"
import { cn } from "@/lib/utils"

type StockTableProps = {
  boardId: string
  boardName: string
  /** admin ou junta_administrativa: cria, edita, troca o status e remove. */
  canManage: boolean
  items: StockItem[]
  /** Candidatos a responsável (só vem para quem gerencia). */
  people: readonly StockPerson[]
}

const EMPTY = <span aria-label="Não informado">—</span>

/** A tabela de estoque: busca, filtro por status, ordenação e o modal. */
export function StockTable({
  boardId,
  boardName,
  canManage,
  items,
  people,
}: StockTableProps) {
  const [optimisticItems, applyStatus] = useOptimistic(
    items,
    (current, change: { id: string; status: StockStatus }) =>
      current.map((item) =>
        item.id === change.id ? { ...item, status: change.status } : item
      )
  )
  const [, startTransition] = useTransition()
  const [query, setQuery] = useState("")
  const [filter, setFilter] = useState<StockStatus | "all">("all")
  const [sort, setSort] = useState<StockSort>(null)
  const [form, setForm] = useState<StockFormTarget | null>(null)
  const [error, setError] = useState<string | null>(null)

  // A contagem de cada filtro vale para o que a busca encontrou.
  const matches = useMemo(
    () => filterItems(optimisticItems, query, "all"),
    [optimisticItems, query]
  )
  const visible = useMemo(
    () => sortItems(filterItems(optimisticItems, query, filter), sort),
    [optimisticItems, query, filter, sort]
  )

  function changeStatus(item: StockItem, status: StockStatus) {
    setError(null)
    startTransition(async () => {
      applyStatus({ id: item.id, status })

      const result = await setStockItemStatus(boardId, item.id, status)

      if (result.error) {
        setError(result.error)
      }
    })
  }

  return (
    <>
      <PageHeader
        scope={boardName}
        title="Estoque"
        actions={
          canManage ? (
            <Button
              type="button"
              className="h-8"
              onClick={() => setForm({ kind: "create" })}
            >
              <Plus />
              Novo item
            </Button>
          ) : null
        }
      />
      {/* Tela única: a barra fica fixa e as linhas rolam dentro da tabela; a página não rola. */}
      <PageContent className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative min-w-52 flex-1 @2xl:max-w-sm">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              type="search"
              value={query}
              placeholder="Buscar item"
              aria-label="Buscar item"
              onChange={(event) => setQuery(event.target.value)}
              className="min-h-10 pl-9 font-normal"
            />
          </div>
          <div
            role="group"
            aria-label="Filtrar por status"
            className="flex flex-wrap gap-1.5"
          >
            <FilterButton
              active={filter === "all"}
              count={matches.length}
              label="Todos"
              onClick={() => setFilter("all")}
            />
            {STOCK_STATUSES.map((status) => (
              <FilterButton
                key={status.status}
                active={filter === status.status}
                count={
                  matches.filter((item) => item.status === status.status).length
                }
                label={status.label}
                onClick={() => setFilter(status.status)}
              />
            ))}
          </div>
        </div>

        {error ? (
          <p
            role="alert"
            className="rounded-lg border border-destructive bg-card px-3 py-2 text-sm font-semibold text-destructive"
          >
            {error}
          </p>
        ) : null}

        <div className="min-h-0 flex-1 overflow-auto rounded-card border bg-card shadow-card">
          <table className="w-full border-collapse text-left text-sm">
            <thead className="sticky top-0 z-10 bg-card shadow-[0_1px_0_var(--border)]">
              <tr>
                <SortHeader
                  label="Item"
                  sort={sort}
                  sortKey="name"
                  onSort={setSort}
                />
                <SortHeader
                  label="Status"
                  sort={sort}
                  sortKey="status"
                  onSort={setSort}
                />
                <SortHeader
                  className="hidden @2xl:table-cell"
                  label="Responsável"
                  sort={sort}
                  sortKey="assignee"
                  onSort={setSort}
                />
                <SortHeader
                  align="right"
                  className="hidden @2xl:table-cell"
                  label="Preço"
                  sort={sort}
                  sortKey="price"
                  onSort={setSort}
                />
                <th
                  scope="col"
                  className="hidden px-3 py-2.5 font-semibold @4xl:table-cell"
                >
                  Detalhes
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((item) => (
                <tr
                  key={item.id}
                  tabIndex={0}
                  onClick={() => setForm({ kind: "edit", item })}
                  onKeyDown={(event) => {
                    if (
                      event.key === "Enter" &&
                      event.target === event.currentTarget
                    ) {
                      setForm({ kind: "edit", item })
                    }
                  }}
                  className="cursor-pointer border-t hover:bg-accent"
                >
                  <td className="max-w-0 min-w-40 px-3 py-2.5 font-semibold [overflow-wrap:anywhere]">
                    {item.name}
                    {/* Celular: preço e responsável sob o nome (as colunas não cabem). */}
                    <span className="mt-0.5 block text-xs font-normal text-muted-foreground tabular-nums @2xl:hidden">
                      {[
                        item.price === null ? null : formatMoney(item.price),
                        item.assignee?.name,
                      ]
                        .filter(Boolean)
                        .join(" · ") || null}
                    </span>
                  </td>
                  <td className="px-3 py-2.5">
                    <StockStatusCell
                      canManage={canManage}
                      item={item}
                      onChange={(status) => changeStatus(item, status)}
                    />
                  </td>
                  <td className="hidden px-3 py-2.5 @2xl:table-cell">
                    {item.assignee?.name ?? EMPTY}
                  </td>
                  <td className="hidden px-3 py-2.5 text-right whitespace-nowrap tabular-nums @2xl:table-cell">
                    {item.price === null ? EMPTY : formatMoney(item.price)}
                  </td>
                  <td className="hidden max-w-xs truncate px-3 py-2.5 text-muted-foreground @4xl:table-cell">
                    {item.details || EMPTY}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {visible.length === 0 ? (
            <p className="p-8 text-center text-sm text-muted-foreground">
              {optimisticItems.length === 0
                ? "Nenhum item no estoque ainda."
                : "Nenhum item encontrado."}
            </p>
          ) : null}
        </div>
      </PageContent>

      <StockFormModal
        boardId={boardId}
        people={people}
        readOnly={!canManage}
        target={form}
        onClose={() => setForm(null)}
      />
    </>
  )
}

function FilterButton({
  active,
  count,
  label,
  onClick,
}: {
  active: boolean
  count: number
  label: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        "inline-flex min-h-8 items-center gap-1.5 rounded-pill border px-3 text-sm font-semibold pointer-coarse:min-h-11",
        active
          ? "border-brand bg-brand text-on-brand"
          : "bg-card hover:bg-accent"
      )}
    >
      {label}
      <span
        className={cn(
          "text-xs",
          active ? "text-on-brand" : "text-muted-foreground"
        )}
      >
        {count}
      </span>
    </button>
  )
}

function SortHeader({
  align = "left",
  className,
  label,
  onSort,
  sort,
  sortKey,
}: {
  align?: "left" | "right"
  className?: string
  label: string
  onSort: (sort: StockSort) => void
  sort: StockSort
  sortKey: SortKey
}) {
  const active = sort?.key === sortKey
  const Icon = !active
    ? ArrowUpDown
    : sort.direction === "asc"
      ? ArrowUp
      : ArrowDown

  return (
    <th
      scope="col"
      aria-sort={
        active
          ? sort.direction === "asc"
            ? "ascending"
            : "descending"
          : "none"
      }
      className={cn(
        "px-1.5 py-1",
        align === "right" && "text-right",
        className
      )}
    >
      <button
        type="button"
        onClick={() => onSort(nextSort(sort, sortKey))}
        className={cn(
          "inline-flex min-h-8 items-center gap-1 rounded-lg px-1.5 font-semibold hover:bg-accent pointer-coarse:min-h-11",
          align === "right" && "flex-row-reverse"
        )}
      >
        {label}
        <Icon
          className={cn(
            "size-3.5",
            active ? "text-accent-ink" : "text-muted-foreground"
          )}
        />
      </button>
    </th>
  )
}
