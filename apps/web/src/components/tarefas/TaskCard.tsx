"use client"

import type { KeyboardEvent } from "react"
import { useSortable } from "@dnd-kit/sortable"
import { CSS } from "@dnd-kit/utilities"
import { MoreHorizontal } from "lucide-react"

import {
  TASK_COLUMNS,
  tagStyle,
  type Task,
  type TaskStatus,
} from "@/components/tarefas/task-data"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { initialsOf } from "@/lib/initials"
import { cn } from "@/lib/utils"

const MAX_AVATARS = 3

type TaskCardProps = {
  /** Quem gerencia: arrasta e usa o menu "Mover para". */
  canManage: boolean
  onMove: (status: TaskStatus) => void
  onOpen: () => void
  task: Task
}

/** Cartão que arrasta (dnd-kit) e abre a tarefa ao clicar ou teclar Enter. */
export function TaskCard({ canManage, onMove, onOpen, task }: TaskCardProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ disabled: !canManage, id: task.id })

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    listeners?.onKeyDown?.(event)

    // Espaço pega o cartão (dnd-kit); Enter abre a tarefa.
    if (event.key === "Enter" && event.target === event.currentTarget) {
      onOpen()
    }
  }

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn("touch-manipulation", isDragging && "opacity-40")}
      // Só quem gerencia arrasta: para os outros o cartão é só um botão que abre os detalhes
      // (sem aria-disabled, sem "sortable" e sem instruções de arrastar).
      {...(canManage ? attributes : {})}
      {...(canManage ? listeners : {})}
      role="button"
      tabIndex={0}
      aria-label={`Tarefa: ${task.name}`}
      onClick={onOpen}
      onKeyDown={handleKeyDown}
    >
      <TaskCardBody
        menu={canManage ? <MoveMenu task={task} onMove={onMove} /> : null}
        task={task}
      />
    </div>
  )
}

/** O visual do cartão, sem lógica de arrastar (também é o que flutua durante o arrastar). */
export function TaskCardBody({
  menu,
  task,
}: {
  menu?: React.ReactNode
  task: Task
}) {
  const extra = task.assignees.length - MAX_AVATARS

  return (
    <article className="cursor-pointer rounded-lg border bg-card p-3 text-left shadow-sm hover:border-input">
      <div className="flex items-start gap-1">
        <h3 className="min-w-0 flex-1 font-sans text-sm font-bold [overflow-wrap:anywhere]">
          {task.name}
        </h3>
        {menu}
      </div>

      {task.description ? (
        <p className="mt-1 line-clamp-2 text-xs [overflow-wrap:anywhere] text-muted-foreground">
          {task.description}
        </p>
      ) : null}

      {task.tags.length > 0 ? (
        <ul className="mt-2 flex flex-wrap gap-1">
          {task.tags.map((tag) => (
            <li
              key={tag}
              className={cn(
                "rounded-pill px-2 text-xs leading-5 font-semibold",
                tagStyle(tag)
              )}
            >
              {tag}
            </li>
          ))}
        </ul>
      ) : null}

      {task.assignees.length > 0 ? (
        <div className="mt-2 flex items-center">
          <span className="sr-only">
            Responsáveis:{" "}
            {task.assignees.map((person) => person.name).join(", ")}
          </span>
          <div aria-hidden="true" className="flex -space-x-1.5">
            {task.assignees.slice(0, MAX_AVATARS).map((person) => (
              <span
                key={person.id}
                title={person.name}
                className="flex size-6 items-center justify-center rounded-pill bg-brand text-[0.625rem] font-bold text-on-brand ring-2 ring-card"
              >
                {initialsOf(person.name)}
              </span>
            ))}
            {extra > 0 ? (
              <span className="flex size-6 items-center justify-center rounded-pill bg-muted text-[0.625rem] font-bold ring-2 ring-card">
                +{extra}
              </span>
            ) : null}
          </div>
        </div>
      ) : null}
    </article>
  )
}

/** Alternativa ao arrastar: "Mover para" outra coluna (fim da coluna), por teclado, toque ou mouse. */
function MoveMenu({
  onMove,
  task,
}: {
  onMove: (status: TaskStatus) => void
  task: Task
}) {
  return (
    // Não abre a tarefa nem começa a arrastar ao usar o menu.
    <div
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => event.stopPropagation()}
      onMouseDown={(event) => event.stopPropagation()}
      onTouchStart={(event) => event.stopPropagation()}
    >
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={`Mover ${task.name} para outra coluna`}
          >
            <MoreHorizontal />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-52">
          {TASK_COLUMNS.filter((column) => column.status !== task.status).map(
            (column) => (
              <DropdownMenuItem
                key={column.status}
                onSelect={() => onMove(column.status)}
              >
                Mover para {column.label}
              </DropdownMenuItem>
            )
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  )
}
