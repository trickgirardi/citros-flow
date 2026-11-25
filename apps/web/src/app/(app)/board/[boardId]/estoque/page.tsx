import { StockTable } from "@/components/estoque/StockTable"
import { createApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"
import { getBoard } from "@/lib/boards"

export default async function EstoquePage({
  params,
}: {
  params: Promise<{ boardId: string }>
}) {
  const { boardId } = await params
  const board = await getBoard(boardId)
  const api = await createApiClient()
  const path = { boardId: board.id }

  // Todo papel no board lê o estoque; a lista de candidatos a responsável só serve a quem gerencia.
  const [items, people] = await Promise.all([
    api
      .GET("/api/boards/{boardId}/stock-items", { params: { path } })
      .then(readData),
    board.canManageTasks
      ? api
          .GET("/api/boards/{boardId}/task-assignees", { params: { path } })
          .then(readData)
      : [],
  ])

  return (
    <StockTable
      boardId={board.id}
      boardName={board.name}
      canManage={board.canManageTasks}
      items={items}
      people={people}
    />
  )
}
