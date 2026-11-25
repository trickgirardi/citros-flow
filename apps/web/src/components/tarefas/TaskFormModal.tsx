"use client"

import { useActionState, useState, useTransition } from "react"

import {
  createTask,
  deleteTask,
  updateTask,
  type TaskFormState,
} from "@/app/(app)/board/[boardId]/tarefas/actions"
import { TagInput } from "@/components/tarefas/TagInput"
import {
  TASK_COLUMNS,
  TASK_LIMITS,
  TASK_PRIORITIES,
  type Task,
  type TaskAssignee,
  type TaskStatus,
} from "@/components/tarefas/task-data"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"

const INITIAL_STATE: TaskFormState = { error: null, success: false }

/** Criar na coluna `status`, ou abrir uma tarefa existente (editar; leitura para quem não gerencia). */
export type TaskFormTarget =
  | { kind: "create"; status: TaskStatus }
  | { kind: "edit"; task: Task }

type TaskFormModalProps = {
  /** Pessoas que podem ser responsáveis (só vem para quem gerencia tarefas). */
  assignees: readonly TaskAssignee[]
  boardId: string
  onClose: () => void
  /** Sem permissão de escrita: tudo desabilitado, só "Fechar". */
  readOnly: boolean
  suggestions: string[]
  target: TaskFormTarget | null
}

export function TaskFormModal({
  onClose,
  target,
  ...props
}: TaskFormModalProps) {
  return (
    <Dialog open={target !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        {/* O formulário só existe com o modal aberto: ao fechar, o que foi digitado some. */}
        {target ? (
          <TaskForm onClose={onClose} target={target} {...props} />
        ) : null}
      </DialogContent>
    </Dialog>
  )
}

function TaskForm({
  assignees,
  boardId,
  onClose,
  readOnly,
  suggestions,
  target,
}: Omit<TaskFormModalProps, "target"> & { target: TaskFormTarget }) {
  const task = target.kind === "edit" ? target.task : null
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [isDeleting, startDelete] = useTransition()
  const [state, formAction, isPending] = useActionState(
    async (current: TaskFormState, formData: FormData) => {
      const next = await (task ? updateTask : createTask)(current, formData)

      if (next.success) {
        onClose()
      }

      return next
    },
    INITIAL_STATE
  )

  // Quem já é responsável aparece mesmo que tenha saído da lista de candidatos.
  const people = [
    ...assignees,
    ...(task?.assignees.filter(
      (assignee) => !assignees.some((person) => person.id === assignee.id)
    ) ?? []),
  ]
  const busy = isPending || isDeleting

  function handleDelete() {
    if (!task || !window.confirm("Remover esta tarefa?")) {
      return
    }

    setDeleteError(null)
    startDelete(async () => {
      const result = await deleteTask(boardId, task.id)

      if (result.error) {
        setDeleteError(result.error)
      } else {
        onClose()
      }
    })
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>
          {readOnly ? "Tarefa" : task ? "Editar tarefa" : "Nova tarefa"}
        </DialogTitle>
        <DialogDescription>
          {readOnly
            ? "Você pode ver os detalhes, mas não alterar a tarefa."
            : "Nome e status são obrigatórios."}
        </DialogDescription>
      </DialogHeader>

      <form action={formAction} className="grid gap-4">
        <input type="hidden" name="boardId" value={boardId} />
        {task ? <input type="hidden" name="taskId" value={task.id} /> : null}

        <fieldset disabled={readOnly} className="grid min-w-0 gap-4">
          <label className="grid gap-1.5 text-base font-semibold">
            Nome
            <Input
              name="name"
              type="text"
              required
              maxLength={TASK_LIMITS.name}
              autoFocus={!readOnly}
              defaultValue={task?.name}
              className="font-normal"
            />
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Descrição
            <Textarea
              name="description"
              maxLength={TASK_LIMITS.description}
              defaultValue={task?.description}
              className="font-normal"
            />
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Status
            <select
              name="status"
              required
              defaultValue={
                task?.status ??
                (target.kind === "create" ? target.status : "aFazer")
              }
              className="min-h-11 rounded-field border border-input bg-card px-4 py-2 text-base font-normal disabled:opacity-60"
            >
              {TASK_COLUMNS.map((column) => (
                <option key={column.status} value={column.status}>
                  {column.label}
                </option>
              ))}
            </select>
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Prioridade
            <select
              name="priority"
              defaultValue={task?.priority ?? ""}
              className="min-h-11 rounded-field border border-input bg-card px-4 py-2 text-base font-normal disabled:opacity-60"
            >
              <option value="">Sem prioridade</option>
              {[...TASK_PRIORITIES].reverse().map((item) => (
                <option key={item.priority} value={item.priority}>
                  {item.label}
                </option>
              ))}
            </select>
          </label>

          <div className="grid gap-1.5">
            <p className="text-base font-semibold">Responsáveis</p>
            {people.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {readOnly
                  ? "Sem responsáveis."
                  : "Nenhuma pessoa disponível neste board."}
              </p>
            ) : (
              <ul className="grid max-h-40 gap-1 overflow-y-auto rounded-field border p-2">
                {people.map((person) => (
                  <li key={person.id}>
                    <label className="flex min-h-9 items-center gap-2 rounded-lg px-2 text-base hover:bg-accent">
                      <input
                        type="checkbox"
                        name="assigneeIds"
                        value={person.id}
                        defaultChecked={task?.assignees.some(
                          (assignee) => assignee.id === person.id
                        )}
                        className="size-4"
                      />
                      <span className="min-w-0 truncate">{person.name}</span>
                    </label>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="grid gap-1.5">
            <p className="text-base font-semibold">Tags</p>
            <TagInput
              defaultTags={task?.tags ?? []}
              readOnly={readOnly}
              suggestions={suggestions}
            />
          </div>
        </fieldset>

        {state.error || deleteError ? (
          <p role="alert" className="text-sm font-semibold text-destructive">
            {state.error ?? deleteError}
          </p>
        ) : null}

        <div className="flex flex-wrap justify-end gap-2">
          {task && !readOnly ? (
            <Button
              type="button"
              variant="ghost"
              size="lg"
              disabled={busy}
              onClick={handleDelete}
              className="mr-auto text-destructive hover:bg-destructive/10"
            >
              {isDeleting ? "Removendo..." : "Remover"}
            </Button>
          ) : null}
          <Button type="button" variant="outline" size="lg" onClick={onClose}>
            {readOnly ? "Fechar" : "Cancelar"}
          </Button>
          {readOnly ? null : (
            <Button type="submit" size="lg" disabled={busy}>
              {isPending ? "Salvando..." : "Salvar"}
            </Button>
          )}
        </div>
      </form>
    </>
  )
}
