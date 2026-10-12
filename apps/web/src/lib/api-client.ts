import "server-only"

import { cookies } from "next/headers"
import createClient from "openapi-fetch"

import type { paths } from "@/lib/api/schema"

// Chamadas server-side vão direto para a API, sem passar pelo proxy/rewrite.
// (O mesmo padrão existe em next.config.ts, onde é lido em tempo de build.)
const API_INTERNAL_URL = process.env.API_INTERNAL_URL ?? "http://localhost:5080"

/**
 * Cliente tipado da API (tipos gerados do OpenAPI com `pnpm gen:api`).
 * Repassa o cookie de sessão do request atual, que é como a API sabe quem é o usuário.
 * Só para Server Components, Server Actions e Route Handlers.
 */
export async function createApiClient() {
  const cookieStore = await cookies()

  return createClient<paths>({
    baseUrl: API_INTERNAL_URL,
    headers: { Cookie: cookieStore.toString() },
    // Dados autenticados e financeiros: nunca servir de cache.
    cache: "no-store",
  })
}

/** Cliente sem cookie, para endpoints públicos (link compartilhado). Nunca manda a sessão do usuário. */
export function createPublicApiClient() {
  return createClient<paths>({ baseUrl: API_INTERNAL_URL, cache: "no-store" })
}
