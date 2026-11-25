import { KanbanBoard } from "@/components/tarefas/KanbanBoard"
import { createApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"
import { getBoard } from "@/lib/boards"

export default async function TarefasPage({
  params,
}: {
  params: Promise<{ boardId: string }>
}) {
  const { boardId } = await params
  const board = await getBoard(boardId)
  const api = await createApiClient()
  const path = { boardId: board.id }

  // Todo papel no board lê as tarefas; a lista de candidatos a responsável só serve a quem gerencia.
  const [tasks, assignees] = await Promise.all([
    api.GET("/api/boards/{boardId}/tasks", { params: { path } }).then(readData),
    board.canManageTasks
      ? api
          .GET("/api/boards/{boardId}/task-assignees", { params: { path } })
          .then(readData)
      : [],
  ])

  return (
    <KanbanBoard
      assignees={assignees}
      boardId={board.id}
      boardName={board.name}
      canManage={board.canManageTasks}
      tasks={tasks}
    />
  )
}
