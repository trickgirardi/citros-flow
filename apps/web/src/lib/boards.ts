import "server-only"

import { cache } from "react"

import { createApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"

/**
 * Board (id, nome, canMutate). Em cache por request, porque o layout e a página precisam dele.
 * Board inexistente ou que o usuário não vê vira 404.
 */
export const getBoard = cache(async (boardId: string) => {
  const api = await createApiClient()

  return readData(
    await api.GET("/api/boards/{boardId}", { params: { path: { boardId } } })
  )
})

/** Boards que o usuário vê (id e nome), para a troca de board no menu. Em cache por request. */
export const listBoards = cache(async () => {
  const api = await createApiClient()

  return readData(await api.GET("/api/boards"))
})
