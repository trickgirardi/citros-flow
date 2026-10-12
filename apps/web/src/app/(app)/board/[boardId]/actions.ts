"use server";

import { revalidatePath } from "next/cache";

import { createApiClient } from "@/lib/api-client";
import { callApi } from "@/lib/api-result";
import { normalizeMoneyInput } from "@/lib/money";

// Todas as escritas vão para a API, que valida, autoriza (404/403) e grava. Aqui só se lê o
// formulário, se normaliza o valor ("1.234,56" -> "1234.56") e se traduz a resposta em texto de tela.

export type TransactionFormState = {
  error: string | null;
  success: boolean;
};

export type DeleteTransactionState = TransactionFormState;
export type UpdateTransactionDescriptionState = TransactionFormState;

export type BulkUpdateTransactionCategoryState = TransactionFormState & {
  updatedCount: number;
};

export type CreateShareLinkState = TransactionFormState & {
  url: string | null;
};

export type ImportNubankCsvState = TransactionFormState & {
  importedCount: number;
};

export type FinancialReserveFormState = TransactionFormState;
export type DeleteFinancialReserveState = TransactionFormState;

const INVALID_TRANSACTION_FORM = "Preencha todos os campos com valores validos.";
const INVALID_RESERVE_FORM = "Preencha nome e valor com dados validos.";

function parseTransactionType(
  value: FormDataEntryValue | null
): "entrada" | "saida" | null {
  const type = value?.toString();

  return type === "entrada" || type === "saida" ? type : null;
}

function parseRequiredText(value: FormDataEntryValue | null) {
  const text = value?.toString().trim();

  return text ? text : null;
}

function parseDate(value: FormDataEntryValue | null) {
  const date = value?.toString();

  if (!date || Number.isNaN(new Date(`${date}T00:00:00`).getTime())) {
    return null;
  }

  return date;
}

function getSafeOrigin(value: FormDataEntryValue | null) {
  const origin = value?.toString();

  if (!origin || !/^https?:\/\/[^/]+$/.test(origin)) {
    return null;
  }

  return origin;
}

/** Corpo de criar/editar transação, ou null se o formulário estiver incompleto. */
function parseTransactionBody(formData: FormData) {
  const type = parseTransactionType(formData.get("type"));
  const amount = normalizeMoneyInput(formData.get("amount")?.toString() ?? "");
  const description = parseRequiredText(formData.get("description"));
  const category = parseRequiredText(formData.get("category"));
  const date = parseDate(formData.get("date"));

  if (!type || !amount || !description || !category || !date) {
    return null;
  }

  return { amount, category, date, description, type };
}

function parseReserveBody(formData: FormData) {
  const name = parseRequiredText(formData.get("name"));
  const amount = normalizeMoneyInput(formData.get("amount")?.toString() ?? "");

  return name && amount ? { amount, name } : null;
}

const SUCCESS: TransactionFormState = { error: null, success: true };

function failure(error: string): TransactionFormState {
  return { error, success: false };
}

export async function createTransaction(
  _: TransactionFormState,
  formData: FormData
): Promise<TransactionFormState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const body = parseTransactionBody(formData);

  if (!boardId || !body) {
    return failure(INVALID_TRANSACTION_FORM);
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.POST("/api/boards/{boardId}/transactions", {
      body,
      params: { path: { boardId } },
    }),
    "Nao foi possivel salvar a transacao."
  );

  if (error) {
    return failure(error);
  }

  revalidatePath(`/board/${boardId}`);

  return SUCCESS;
}

export async function updateTransaction(
  _: TransactionFormState,
  formData: FormData
): Promise<TransactionFormState> {
  const transactionId = parseRequiredText(formData.get("transactionId"));
  const boardId = parseRequiredText(formData.get("boardId"));
  const body = parseTransactionBody(formData);

  if (!transactionId || !boardId || !body) {
    return failure(INVALID_TRANSACTION_FORM);
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.PUT("/api/boards/{boardId}/transactions/{id}", {
      body,
      params: { path: { boardId, id: transactionId } },
    }),
    "Nao foi possivel atualizar a transacao."
  );

  if (error) {
    return failure(error);
  }

  revalidatePath(`/board/${boardId}`);

  return SUCCESS;
}

export async function updateTransactionDescription(
  _: UpdateTransactionDescriptionState,
  formData: FormData
): Promise<UpdateTransactionDescriptionState> {
  const transactionId = parseRequiredText(formData.get("transactionId"));
  const boardId = parseRequiredText(formData.get("boardId"));
  const description = parseRequiredText(formData.get("description"));

  if (!transactionId || !boardId || !description || description.length > 120) {
    return failure("Descricao invalida.");
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.PATCH("/api/boards/{boardId}/transactions/{id}/description", {
      body: { description },
      params: { path: { boardId, id: transactionId } },
    }),
    "Nao foi possivel atualizar a descricao."
  );

  if (error) {
    return failure(error);
  }

  revalidatePath(`/board/${boardId}`);

  return SUCCESS;
}

export async function updateTransactionsCategory(
  _: BulkUpdateTransactionCategoryState,
  formData: FormData
): Promise<BulkUpdateTransactionCategoryState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const category = parseRequiredText(formData.get("category"));
  const ids = Array.from(
    new Set(
      formData
        .getAll("transactionIds")
        .map((value) => value.toString().trim())
        .filter((value) => value.length > 0)
    )
  );

  if (!boardId || !category || category.length > 80 || ids.length === 0) {
    return {
      ...failure("Selecione transacoes e uma categoria valida."),
      updatedCount: 0,
    };
  }

  const api = await createApiClient();
  const { data, error } = await callApi(
    api.PATCH("/api/boards/{boardId}/transactions/category", {
      body: { category, ids },
      params: { path: { boardId } },
    }),
    "Nao foi possivel atualizar as categorias."
  );

  if (error) {
    return { ...failure(error), updatedCount: 0 };
  }

  revalidatePath(`/board/${boardId}`);

  return { ...SUCCESS, updatedCount: data?.updatedCount ?? 0 };
}

export async function deleteTransaction(
  _: DeleteTransactionState,
  formData: FormData
): Promise<DeleteTransactionState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const transactionId = parseRequiredText(formData.get("transactionId"));

  if (!boardId || !transactionId) {
    return failure("Transacao invalida.");
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.DELETE("/api/boards/{boardId}/transactions/{id}", {
      params: { path: { boardId, id: transactionId } },
    }),
    "Nao foi possivel remover a transacao."
  );

  if (error) {
    return failure(error);
  }

  revalidatePath(`/board/${boardId}`);

  return SUCCESS;
}

export async function importNubankCsv(
  _: ImportNubankCsvState,
  formData: FormData
): Promise<ImportNubankCsvState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const file = formData.get("csvFile");

  if (!boardId || !file || typeof file === "string" || file.size === 0) {
    return {
      ...failure("Selecione um arquivo CSV valido."),
      importedCount: 0,
    };
  }

  // O parser do extrato mora na API. O tipo gerado descreve o arquivo como string; o
  // bodySerializer monta o multipart de verdade (campo "file").
  const api = await createApiClient();
  const { data, error } = await callApi(
    api.POST("/api/boards/{boardId}/transactions/import/nubank", {
      body: { file: "" },
      bodySerializer: () => {
        const multipart = new FormData();
        multipart.append("file", file);
        return multipart;
      },
      params: { path: { boardId } },
    }),
    "Nao foi possivel importar o CSV."
  );

  if (error) {
    return { ...failure(error), importedCount: 0 };
  }

  revalidatePath(`/board/${boardId}`);

  return { ...SUCCESS, importedCount: data?.importedCount ?? 0 };
}

export async function createShareLink(
  _: CreateShareLinkState,
  formData: FormData
): Promise<CreateShareLinkState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const origin = getSafeOrigin(formData.get("origin"));

  if (!boardId || !origin) {
    return { ...failure("Nao foi possivel gerar o link."), url: null };
  }

  const api = await createApiClient();
  const { data, error } = await callApi(
    api.POST("/api/boards/{boardId}/share-links", {
      params: { path: { boardId } },
    }),
    "Nao foi possivel gerar o link."
  );

  if (error || !data) {
    return { ...failure(error ?? "Nao foi possivel gerar o link."), url: null };
  }

  return { ...SUCCESS, url: `${origin}/share/${data.token}` };
}

export async function createFinancialReserve(
  _: FinancialReserveFormState,
  formData: FormData
): Promise<FinancialReserveFormState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const body = parseReserveBody(formData);

  if (!boardId || !body) {
    return failure(INVALID_RESERVE_FORM);
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.POST("/api/boards/{boardId}/reserves", {
      body,
      params: { path: { boardId } },
    }),
    "Nao foi possivel salvar a reserva."
  );

  if (error) {
    return failure(error);
  }

  revalidatePath(`/board/${boardId}`);

  return SUCCESS;
}

export async function updateFinancialReserve(
  _: FinancialReserveFormState,
  formData: FormData
): Promise<FinancialReserveFormState> {
  const reserveId = parseRequiredText(formData.get("reserveId"));
  const boardId = parseRequiredText(formData.get("boardId"));
  const body = parseReserveBody(formData);

  if (!reserveId || !boardId || !body) {
    return failure(INVALID_RESERVE_FORM);
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.PUT("/api/boards/{boardId}/reserves/{id}", {
      body,
      params: { path: { boardId, id: reserveId } },
    }),
    "Nao foi possivel atualizar a reserva."
  );

  if (error) {
    return failure(error);
  }

  revalidatePath(`/board/${boardId}`);

  return SUCCESS;
}

export async function deleteFinancialReserve(
  _: DeleteFinancialReserveState,
  formData: FormData
): Promise<DeleteFinancialReserveState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const reserveId = parseRequiredText(formData.get("reserveId"));

  if (!boardId || !reserveId) {
    return failure("Reserva invalida.");
  }

  const api = await createApiClient();
  const { error } = await callApi(
    api.DELETE("/api/boards/{boardId}/reserves/{id}", {
      params: { path: { boardId, id: reserveId } },
    }),
    "Nao foi possivel remover a reserva."
  );

  if (error) {
    return failure(error);
  }

  revalidatePath(`/board/${boardId}`);

  return SUCCESS;
}
