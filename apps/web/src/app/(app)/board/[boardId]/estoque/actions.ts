"use server"

import { revalidatePath } from "next/cache"

import type { StockStatus } from "@/components/estoque/stock-data"
import { createApiClient } from "@/lib/api-client"
import { callApi } from "@/lib/api-result"
import { normalizeMoneyInput } from "@/lib/money"

// A API valida (item, status, preço, responsável) e autoriza (403 sem a role junta_administrativa/admin).
// Aqui só se lê o formulário, se normaliza o preço ("1.234,56" -> "1234.56") e se traduz a resposta.

export type StockFormState = {
  error: string | null
  success: boolean
}

const STATUSES: readonly string[] = [
  "emEstoque",
  "aCaminho",
  "comprar",
  "comprarComUrgencia",
]

function failure(error: string): StockFormState {
  return { error, success: false }
}

function parseText(value: FormDataEntryValue | null) {
  return value?.toString().trim() ?? ""
}

/** Corpo de criar/editar, ou a mensagem de erro (item e status obrigatórios, preço válido). */
function parseBody(formData: FormData) {
  const name = parseText(formData.get("name"))
  const status = parseText(formData.get("status"))
  const priceText = parseText(formData.get("price"))
  const price = priceText ? normalizeMoneyInput(priceText) : null

  if (!name || !STATUSES.includes(status)) {
    return { error: "Informe o item e o status." }
  }

  if (priceText && price === null) {
    return { error: "Preco invalido. Use numeros, ex.: 1.234,56." }
  }

  return {
    body: {
      assigneeId: parseText(formData.get("assigneeId")) || null,
      details: parseText(formData.get("details")),
      name,
      price,
      status: status as StockStatus,
    },
  }
}

function revalidateStock(boardId: string) {
  revalidatePath(`/board/${boardId}/estoque`)
}

export async function createStockItem(
  _: StockFormState,
  formData: FormData
): Promise<StockFormState> {
  const boardId = parseText(formData.get("boardId"))
  const { body, error: invalid } = parseBody(formData)

  if (!boardId || !body) {
    return failure(invalid ?? "Informe o item e o status.")
  }

  const api = await createApiClient()
  const { error } = await callApi(
    api.POST("/api/boards/{boardId}/stock-items", {
      body,
      params: { path: { boardId } },
    }),
    "Nao foi possivel criar o item."
  )

  if (error) {
    return failure(error)
  }

  revalidateStock(boardId)

  return { error: null, success: true }
}

export async function updateStockItem(
  _: StockFormState,
  formData: FormData
): Promise<StockFormState> {
  const boardId = parseText(formData.get("boardId"))
  const itemId = parseText(formData.get("itemId"))
  const { body, error: invalid } = parseBody(formData)

  if (!boardId || !itemId || !body) {
    return failure(invalid ?? "Informe o item e o status.")
  }

  const api = await createApiClient()
  const { error } = await callApi(
    api.PUT("/api/boards/{boardId}/stock-items/{itemId}", {
      body,
      params: { path: { boardId, itemId } },
    }),
    "Nao foi possivel salvar o item."
  )

  if (error) {
    return failure(error)
  }

  revalidateStock(boardId)

  return { error: null, success: true }
}

export async function deleteStockItem(
  boardId: string,
  itemId: string
): Promise<StockFormState> {
  const api = await createApiClient()
  const { error } = await callApi(
    api.DELETE("/api/boards/{boardId}/stock-items/{itemId}", {
      params: { path: { boardId, itemId } },
    }),
    "Nao foi possivel remover o item."
  )

  if (error) {
    return failure(error)
  }

  revalidateStock(boardId)

  return { error: null, success: true }
}

export async function setStockItemStatus(
  boardId: string,
  itemId: string,
  status: StockStatus
): Promise<StockFormState> {
  const api = await createApiClient()
  const { error } = await callApi(
    api.PATCH("/api/boards/{boardId}/stock-items/{itemId}/status", {
      body: { status },
      params: { path: { boardId, itemId } },
    }),
    "Nao foi possivel trocar o status."
  )

  // Em erro também revalida: a tabela volta ao que está salvo.
  revalidateStock(boardId)

  return error ? failure(error) : { error: null, success: true }
}
