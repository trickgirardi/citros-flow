"use client"

import { useDroppable } from "@dnd-kit/core"
import { SortableContext, verticalListSortingStrategy } from "@dnd-kit/sortable"
import { Plus } from "lucide-react"

import { TaskCard } from "@/components/tarefas/TaskCard"
import type { Task, TaskStatus } from "@/components/tarefas/task-data"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

const COLUMN_PREFIX = "column:"

/** Id da coluna como destino de soltar (para soltar numa coluna vazia ou no espaço dela). */
export const columnDropId = (status: TaskStatus) => `${COLUMN_PREFIX}${status}`

/** A coluna de um destino de soltar; null se o destino for um cartão. */
export function statusFromDropId(id: string | number) {
  const value = String(id)

  return value.startsWith(COLUMN_PREFIX)
    ? (value.slice(COLUMN_PREFIX.length) as TaskStatus)
    : null
}

type KanbanColumnProps = {
  canManage: boolean
  label: string
  onCreate: () => void
  onMove: (task: Task, status: TaskStatus) => void
  onOpen: (task: Task) => void
  status: TaskStatus
  tasks: Task[]
}

export function KanbanColumn({
  canManage,
  label,
  onCreate,
  onMove,
  onOpen,
  status,
  tasks,
}: KanbanColumnProps) {
  const { isOver, setNodeRef } = useDroppable({
    disabled: !canManage,
    id: columnDropId(status),
  })

  return (
    <section
      aria-label={label}
      className="flex min-h-0 w-[min(20rem,85%)] shrink-0 snap-center flex-col rounded-card border bg-card shadow-card @3xl:w-auto"
    >
      <header className="flex items-center gap-2 border-b px-3 py-2">
        <h2 className="font-sans text-sm font-bold">{label}</h2>
        <span className="rounded-pill bg-muted px-2 text-xs leading-5 font-semibold text-muted-foreground">
          {tasks.length}
        </span>
        {canManage ? (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="ml-auto"
            aria-label={`Nova tarefa em ${label}`}
            onClick={onCreate}
          >
            <Plus />
          </Button>
        ) : null}
      </header>

      <div
        ref={setNodeRef}
        className={cn(
          "min-h-0 flex-1 overflow-y-auto rounded-b-card p-2 transition-colors",
          isOver && "bg-accent"
        )}
      >
        <SortableContext
          items={tasks.map((task) => task.id)}
          strategy={verticalListSortingStrategy}
        >
          <ul className="flex min-h-12 flex-col gap-2">
            {tasks.map((task) => (
              <li key={task.id}>
                <TaskCard
                  canManage={canManage}
                  task={task}
                  onMove={(next) => onMove(task, next)}
                  onOpen={() => onOpen(task)}
                />
              </li>
            ))}
          </ul>
        </SortableContext>
        {tasks.length === 0 ? (
          <p className="rounded-lg border border-dashed p-4 text-center text-xs text-muted-foreground">
            Nenhuma tarefa
          </p>
        ) : null}
      </div>
    </section>
  )
}
