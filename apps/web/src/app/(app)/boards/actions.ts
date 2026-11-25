"use server";

import { revalidatePath } from "next/cache";

import { createApiClient } from "@/lib/api-client";
import { callApi } from "@/lib/api-result";

// Só admin global gerencia boards: a API decide (403) e valida o nome (400/409). Aqui se lê o
// formulário e se traduz a resposta em texto de tela.

export type BoardFormState = {
  error: string | null;
  success: boolean;
};

const SUCCESS: BoardFormState = { error: null, success: true };

function failure(error: string): BoardFormState {
  return { error, success: false };
}

function parseRequiredText(value: FormDataEntryValue | null) {
  const text = value?.toString().trim();

  return text ? text : null;
}

// A lista de boards está na sidebar (layout do shell), então vale para todas as telas.
function revalidateBoards() {
  revalidatePath("/", "layout");
}

export async function createBoard(
  _: BoardFormState,
  formData: FormData
): Promise<BoardFormState> {
  const name = parseRequiredText(formData.get("name"));

  if (!name) {
    return failure("Informe o nome do board.");
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.POST("/api/boards", { body: { name } }),
    "Nao foi possivel criar o board."
  );

  if (error) {
    return failure(error);
  }

  revalidateBoards();

  return SUCCESS;
}

export async function updateBoard(
  _: BoardFormState,
  formData: FormData
): Promise<BoardFormState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const name = parseRequiredText(formData.get("name"));

  if (!boardId || !name) {
    return failure("Informe o nome do board.");
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.PUT("/api/boards/{boardId}", {
      body: { name },
      params: { path: { boardId } },
    }),
    "Nao foi possivel renomear o board."
  );

  if (error) {
    return failure(error);
  }

  revalidateBoards();

  return SUCCESS;
}

export async function deleteBoard(
  _: BoardFormState,
  formData: FormData
): Promise<BoardFormState> {
  const boardId = parseRequiredText(formData.get("boardId"));

  if (!boardId) {
    return failure("Board invalido.");
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.DELETE("/api/boards/{boardId}", { params: { path: { boardId } } }),
    "Nao foi possivel remover o board."
  );

  if (error) {
    return failure(error);
  }

  revalidateBoards();

  return SUCCESS;
}
