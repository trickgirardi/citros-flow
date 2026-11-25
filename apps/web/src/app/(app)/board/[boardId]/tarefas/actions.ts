"use server"

import { revalidatePath } from "next/cache"

import type { TaskMove, TaskStatus } from "@/components/tarefas/task-data"
import { createApiClient } from "@/lib/api-client"
import { callApi } from "@/lib/api-result"

// A API valida (nome, status, tags, responsáveis) e autoriza (403 sem a role junta_administrativa/admin).
// Aqui só se lê o formulário e se traduz a resposta em texto de tela.

export type TaskFormState = {
  error: string | null
  success: boolean
}

const STATUSES: readonly string[] = ["aFazer", "emAndamento", "concluida"]

function failure(error: string): TaskFormState {
  return { error, success: false }
}

function parseText(value: FormDataEntryValue | null) {
  return value?.toString().trim() ?? ""
}

/** Corpo de criar/editar, ou null se faltar nome ou status (obrigatórios). */
function parseTaskBody(formData: FormData) {
  const name = parseText(formData.get("name"))
  const status = parseText(formData.get("status"))

  if (!name || !STATUSES.includes(status)) {
    return null
  }

  return {
    assigneeIds: formData.getAll("assigneeIds").map((id) => id.toString()),
    description: parseText(formData.get("description")),
    name,
    status: status as TaskStatus,
    tags: formData.getAll("tags").map((tag) => tag.toString()),
  }
}

function revalidateTasks(boardId: string) {
  revalidatePath(`/board/${boardId}/tarefas`)
}

export async function createTask(
  _: TaskFormState,
  formData: FormData
): Promise<TaskFormState> {
  const boardId = parseText(formData.get("boardId"))
  const body = parseTaskBody(formData)

  if (!boardId || !body) {
    return failure("Informe o nome e o status da tarefa.")
  }

  const api = await createApiClient()
  const { error } = await callApi(
    api.POST("/api/boards/{boardId}/tasks", {
      body,
      params: { path: { boardId } },
    }),
    "Nao foi possivel criar a tarefa."
  )

  if (error) {
    return failure(error)
  }

  revalidateTasks(boardId)

  return { error: null, success: true }
}

export async function updateTask(
  _: TaskFormState,
  formData: FormData
): Promise<TaskFormState> {
  const boardId = parseText(formData.get("boardId"))
  const taskId = parseText(formData.get("taskId"))
  const body = parseTaskBody(formData)

  if (!boardId || !taskId || !body) {
    return failure("Informe o nome e o status da tarefa.")
  }

  const api = await createApiClient()
  const { error } = await callApi(
    api.PUT("/api/boards/{boardId}/tasks/{taskId}", {
      body,
      params: { path: { boardId, taskId } },
    }),
    "Nao foi possivel salvar a tarefa."
  )

  if (error) {
    return failure(error)
  }

  revalidateTasks(boardId)

  return { error: null, success: true }
}

export async function deleteTask(
  boardId: string,
  taskId: string
): Promise<TaskFormState> {
  const api = await createApiClient()
  const { error } = await callApi(
    api.DELETE("/api/boards/{boardId}/tasks/{taskId}", {
      params: { path: { boardId, taskId } },
    }),
    "Nao foi possivel remover a tarefa."
  )

  if (error) {
    return failure(error)
  }

  revalidateTasks(boardId)

  return { error: null, success: true }
}

export async function moveTaskAction(
  boardId: string,
  move: TaskMove
): Promise<TaskFormState> {
  const api = await createApiClient()
  const { error } = await callApi(
    api.PATCH("/api/boards/{boardId}/tasks/{taskId}/move", {
      body: { index: move.index, status: move.status },
      params: { path: { boardId, taskId: move.id } },
    }),
    "Nao foi possivel mover a tarefa."
  )

  // Em erro também revalida: o quadro volta ao que está salvo.
  revalidateTasks(boardId)

  return error ? failure(error) : { error: null, success: true }
}
