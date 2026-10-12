import { notFound, redirect } from "next/navigation"

const SESSION_EXPIRED_URL = "/login?error=session_expired"

type ApiResult = { data?: unknown; error?: unknown; response: Response }

/**
 * Leitura em Server Component: devolve o dado, ou redireciona para o login (401, sessão expirada),
 * ou renderiza o 404 (board que não existe ou que o usuário não vê). Qualquer outro erro lança.
 */
export function readData<R extends ApiResult>(
  result: R
): NonNullable<R["data"]> {
  if (result.data !== undefined && result.data !== null) return result.data
  if (result.response.status === 401) redirect(SESSION_EXPIRED_URL)
  if (result.response.status === 404) notFound()
  throw new Error(`A API respondeu ${result.response.status}.`)
}

/** Mensagem para o usuário a partir de uma resposta de erro. 401 leva ao login. */
export function apiErrorMessage(
  response: Response,
  error: unknown,
  fallback: string
) {
  if (response.status === 401) redirect(SESSION_EXPIRED_URL)
  if (response.status === 403) return "Voce nao tem permissao para esta acao."
  if (response.status === 404) return "Board ou item nao encontrado."
  // 400: a API devolve ProblemDetails e o `title` já é a mensagem para o usuário.
  if (response.status === 400 && typeof error === "object" && error !== null) {
    const title = (error as { title?: unknown }).title
    if (typeof title === "string" && title) return title
  }

  return fallback
}

/**
 * Escrita em Server Action. Falha de rede ou erro da API viram `error` (texto para a tela);
 * em caso de sucesso vem `error: null` e o corpo da resposta em `data`.
 */
export async function callApi<R extends ApiResult>(
  request: Promise<R>,
  fallback: string
): Promise<
  { error: string; data?: undefined } | { error: null; data: R["data"] }
> {
  let result: R
  try {
    result = await request
  } catch {
    return { error: fallback }
  }

  if (!result.response.ok) {
    return { error: apiErrorMessage(result.response, result.error, fallback) }
  }

  return { error: null, data: result.data }
}
