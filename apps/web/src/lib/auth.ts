import "server-only"

import { cache } from "react"

import { createApiClient } from "@/lib/api-client"

/**
 * Usuário da sessão atual, ou null se não houver sessão válida (a API responde 401).
 * Em cache por request: o layout e a página perguntam, a API é consultada uma vez.
 */
export const getCurrentUser = cache(async () => {
  const api = await createApiClient()
  const { data, response } = await api.GET("/api/me")

  if (data) return data
  if (response.status === 401) return null

  throw new Error(`A API respondeu ${response.status} ao consultar a sessão.`)
})
