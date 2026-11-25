"use client"

import { useMemo, useOptimistic, useState, useTransition } from "react"
import {
  closestCorners,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  MouseSensor,
  pointerWithin,
  TouchSensor,
  useSensor,
  useSensors,
  type Announcements,
  type CollisionDetection,
  type DragEndEvent,
} from "@dnd-kit/core"
import { sortableKeyboardCoordinates } from "@dnd-kit/sortable"
import { Plus } from "lucide-react"

import { moveTaskAction } from "@/app/(app)/board/[boardId]/tarefas/actions"
import { PageContent } from "@/components/layout/page-content"
import { PageHeader } from "@/components/layout/page-header"
import {
  KanbanColumn,
  statusFromDropId,
} from "@/components/tarefas/KanbanColumn"
import { TaskCardBody } from "@/components/tarefas/TaskCard"
import {
  TaskFormModal,
  type TaskFormTarget,
} from "@/components/tarefas/TaskFormModal"
import {
  columnOf,
  moveTask,
  TASK_COLUMNS,
  tagSuggestions,
  type Task,
  type TaskAssignee,
  type TaskMove,
  type TaskStatus,
} from "@/components/tarefas/task-data"
import { Button } from "@/components/ui/button"

type KanbanBoardProps = {
  assignees: readonly TaskAssignee[]
  boardId: string
  boardName: string
  /** admin ou junta_administrativa: cria, edita, move e remove. */
  canManage: boolean
  tasks: Task[]
}

// Soltar sobre um cartão vale mais que soltar sobre a coluna que o contém.
const collisionDetection: CollisionDetection = (args) => {
  const hits = pointerWithin(args)

  if (hits.length === 0) {
    return closestCorners(args)
  }

  const cards = hits.filter((hit) => statusFromDropId(hit.id) === null)

  return cards.length > 0 ? cards : hits
}

/** O quadro: cabeçalho, as três colunas e o modal. É otimista: o cartão muda antes da API responder. */
export function KanbanBoard({
  assignees,
  boardId,
  boardName,
  canManage,
  tasks,
}: KanbanBoardProps) {
  const [optimisticTasks, applyMove] = useOptimistic(tasks, moveTask)
  const [, startTransition] = useTransition()
  const [error, setError] = useState<string | null>(null)
  const [form, setForm] = useState<TaskFormTarget | null>(null)
  const [activeId, setActiveId] = useState<string | null>(null)
  const suggestions = useMemo(() => tagSuggestions(tasks), [tasks])
  const activeTask = optimisticTasks.find((task) => task.id === activeId)

  // Mouse com 6px de folga (clicar abre a tarefa), toque segurando um instante (rolar não arrasta)
  // e teclado só com Espaço (Enter abre a tarefa).
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, {
      activationConstraint: { delay: 200, tolerance: 8 },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
      keyboardCodes: { cancel: ["Escape"], end: ["Space"], start: ["Space"] },
    })
  )

  function commit(move: TaskMove) {
    setError(null)
    startTransition(async () => {
      applyMove(move)

      const result = await moveTaskAction(boardId, move)

      if (result.error) {
        setError(result.error)
      }
    })
  }

  function endOfColumn(task: Task, status: TaskStatus) {
    return columnOf(optimisticTasks, status).filter(
      (item) => item.id !== task.id
    ).length
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    setActiveId(null)

    const task = optimisticTasks.find((item) => item.id === active.id)

    if (!task || !over) {
      return
    }

    const overColumn = statusFromDropId(over.id)
    let status: TaskStatus
    let index: number

    if (overColumn) {
      status = overColumn
      index = endOfColumn(task, status)
    } else {
      const overTask = optimisticTasks.find((item) => item.id === over.id)

      if (!overTask) {
        return
      }

      status = overTask.status

      if (status === task.status) {
        // Mesma coluna: a tarefa assume a posição do cartão sobre o qual foi solta.
        index = columnOf(optimisticTasks, status).findIndex(
          (item) => item.id === overTask.id
        )
      } else {
        // Outra coluna: entra antes do cartão, ou depois se foi solta na metade de baixo dele.
        const dragged = active.rect.current.translated
        const below =
          dragged !== null &&
          dragged.top + dragged.height / 2 >
            over.rect.top + over.rect.height / 2
        const before = columnOf(optimisticTasks, status).findIndex(
          (item) => item.id === overTask.id
        )

        index = before + (below ? 1 : 0)
      }
    }

    const current = columnOf(optimisticTasks, task.status).findIndex(
      (item) => item.id === task.id
    )

    if (status === task.status && index === current) {
      return
    }

    commit({ id: task.id, index, status })
  }

  const taskName = (id: string | number) =>
    optimisticTasks.find((task) => task.id === id)?.name ?? "tarefa"
  const columnName = (id: string | number) => {
    const status =
      statusFromDropId(id) ??
      optimisticTasks.find((task) => task.id === id)?.status

    return TASK_COLUMNS.find((column) => column.status === status)?.label ?? ""
  }
  const announcements: Announcements = {
    onDragCancel: ({ active }) =>
      `Movimento cancelado: ${taskName(active.id)}.`,
    onDragEnd: ({ active, over }) =>
      over
        ? `${taskName(active.id)} solta em ${columnName(over.id)}.`
        : `${taskName(active.id)} voltou ao lugar.`,
    onDragOver: ({ active, over }) =>
      over ? `${taskName(active.id)} sobre ${columnName(over.id)}.` : undefined,
    onDragStart: ({ active }) => `Pegou ${taskName(active.id)}.`,
  }

  return (
    <>
      <PageHeader
        scope={boardName}
        title="Gestão de tarefas"
        actions={
          canManage ? (
            <Button
              type="button"
              className="h-8"
              onClick={() => setForm({ kind: "create", status: "aFazer" })}
            >
              <Plus />
              Nova tarefa
            </Button>
          ) : null
        }
      />
      {/* O quadro é tela única: cada coluna rola por dentro; a página não rola. */}
      <PageContent className="flex flex-col gap-2">
        {error ? (
          <p
            role="alert"
            className="rounded-lg border border-destructive bg-card px-3 py-2 text-sm font-semibold text-destructive"
          >
            {error}
          </p>
        ) : null}

        <DndContext
          // Id fixo: o contador automático do dnd-kit difere entre servidor e navegador (hidratação).
          id="tasks-board"
          accessibility={{
            announcements,
            screenReaderInstructions: {
              draggable:
                "Para pegar a tarefa, tecle Espaço. Use as setas para mover e Espaço para soltar. Esc cancela.",
            },
          }}
          collisionDetection={collisionDetection}
          sensors={sensors}
          onDragCancel={() => setActiveId(null)}
          onDragEnd={handleDragEnd}
          onDragStart={({ active }) => setActiveId(String(active.id))}
        >
          <div className="flex min-h-0 flex-1 snap-x gap-3 overflow-x-auto @3xl:grid @3xl:grid-cols-3 @3xl:overflow-x-visible">
            {TASK_COLUMNS.map((column) => (
              <KanbanColumn
                key={column.status}
                canManage={canManage}
                label={column.label}
                status={column.status}
                tasks={columnOf(optimisticTasks, column.status)}
                onCreate={() =>
                  setForm({ kind: "create", status: column.status })
                }
                onMove={(task, status) =>
                  commit({
                    id: task.id,
                    index: endOfColumn(task, status),
                    status,
                  })
                }
                onOpen={(task) => setForm({ kind: "edit", task })}
              />
            ))}
          </div>
          <DragOverlay>
            {activeTask ? (
              <div className="rotate-2 shadow-card">
                <TaskCardBody task={activeTask} />
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>
      </PageContent>

      <TaskFormModal
        assignees={assignees}
        boardId={boardId}
        readOnly={!canManage}
        suggestions={suggestions}
        target={form}
        onClose={() => setForm(null)}
      />
    </>
  )
}
