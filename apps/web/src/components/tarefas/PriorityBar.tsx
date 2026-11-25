import { ArrowDownWideNarrow } from "lucide-react"

import { FilterButton } from "@/components/FilterButton"
import {
  matchesPriority,
  TASK_PRIORITIES,
  type PriorityFilter,
  type Task,
} from "@/components/tarefas/task-data"
import { cn } from "@/lib/utils"

const FILTERS: readonly { filter: PriorityFilter; label: string }[] = [
  { filter: "all", label: "Todas" },
  ...TASK_PRIORITIES.map((item) => ({
    filter: item.priority,
    label: item.label,
  })),
  { filter: "none", label: "Sem prioridade" },
]

type PriorityBarProps = {
  filter: PriorityFilter
  onFilter: (filter: PriorityFilter) => void
  onSortedChange: (sorted: boolean) => void
  sorted: boolean
  tasks: readonly Task[]
}

/** Filtro por prioridade (com contagem) e o botão "Ordenar por prioridade". Só mudam a visão, não os dados. */
export function PriorityBar({
  filter,
  onFilter,
  onSortedChange,
  sorted,
  tasks,
}: PriorityBarProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <div
        role="group"
        aria-label="Filtrar por prioridade"
        className="flex max-w-full gap-1.5 overflow-x-auto @3xl:flex-wrap"
      >
        {FILTERS.map((item) => (
          <FilterButton
            key={item.filter}
            active={filter === item.filter}
            count={
              tasks.filter((task) => matchesPriority(task, item.filter)).length
            }
            label={item.label}
            onClick={() => onFilter(item.filter)}
          />
        ))}
      </div>
      <button
        type="button"
        aria-pressed={sorted}
        onClick={() => onSortedChange(!sorted)}
        className={cn(
          "inline-flex min-h-8 items-center gap-1.5 rounded-pill border px-3 text-sm font-semibold @3xl:ml-auto pointer-coarse:min-h-11",
          sorted
            ? "border-brand bg-brand text-on-brand"
            : "bg-card hover:bg-accent"
        )}
      >
        <ArrowDownWideNarrow aria-hidden="true" className="size-4" />
        Ordenar por prioridade
      </button>
    </div>
  )
}
