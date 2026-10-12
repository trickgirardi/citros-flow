"use server";

import { revalidatePath } from "next/cache";

import { createShareToken } from "@/lib/share-token";
import { getBoardForCurrentUser } from "@/lib/supabase/queries/boards";
import {
  createFinancialReserveForCurrentUser,
  deleteFinancialReserveForCurrentUser,
  updateFinancialReserveForCurrentUser,
} from "@/lib/supabase/queries/financial-reserves";
import { createBoardShareLinkForCurrentUser } from "@/lib/supabase/queries/share-links";
import {
  createTransactionForCurrentUser,
  createTransactionsForCurrentUser,
  deleteTransactionForCurrentUser,
  type BulkCreateTransactionInput,
  updateTransactionsCategoryForCurrentUser,
  updateTransactionDescriptionForCurrentUser,
  updateTransactionForCurrentUser,
} from "@/lib/supabase/queries/transactions";

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

const VALID_TRANSACTION_TYPES = new Set(["entrada", "saida"]);
const IMPORTED_CATEGORY = "Dados importados";

function parseTransactionType(value: FormDataEntryValue | null) {
  const type = value?.toString();

  if (type !== "entrada" && type !== "saida") {
    return null;
  }

  return type;
}

function parseAmount(value: FormDataEntryValue | null) {
  const normalizedValue = value?.toString().replace(",", ".").trim();
  const amount = Number(normalizedValue);

  if (!Number.isFinite(amount) || amount <= 0) {
    return null;
  }

  return Math.round(amount * 100) / 100;
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

function parseNubankDate(value: string) {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value.trim());

  if (!match) {
    return null;
  }

  const [, day, month, year] = match;
  const date = `${year}-${month}-${day}`;

  if (Number.isNaN(new Date(`${date}T00:00:00`).getTime())) {
    return null;
  }

  return date;
}

function parseNubankAmount(value: string) {
  const amount = Number(value.trim().replace(",", "."));

  if (!Number.isFinite(amount) || amount === 0) {
    return null;
  }

  return {
    amount: Math.round(Math.abs(amount) * 100) / 100,
    type: amount > 0 ? "entrada" : "saida",
  } satisfies Pick<BulkCreateTransactionInput, "amount" | "type">;
}

function parseCsvLine(line: string) {
  const fields: string[] = [];
  let field = "";
  let inQuotes = false;

  for (let index = 0; index < line.length; index += 1) {
    const character = line[index];
    const nextCharacter = line[index + 1];

    if (character === "\"" && inQuotes && nextCharacter === "\"") {
      field += "\"";
      index += 1;
      continue;
    }

    if (character === "\"") {
      inQuotes = !inQuotes;
      continue;
    }

    if (character === "," && !inQuotes) {
      fields.push(field);
      field = "";
      continue;
    }

    field += character;
  }

  fields.push(field);

  return fields.map((value) => value.trim());
}

function parseNubankCsv(content: string): BulkCreateTransactionInput[] {
  const lines = content
    .replace(/^\uFEFF/, "")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  if (lines.length < 2) {
    throw new Error("Arquivo CSV vazio.");
  }

  const header = parseCsvLine(lines[0]).map((field) => field.toLowerCase());

  if (
    header[0] !== "data" ||
    header[1] !== "valor" ||
    header[2] !== "identificador" ||
    header[3] !== "descrição"
  ) {
    throw new Error("Cabecalho CSV invalido.");
  }

  return lines.slice(1).map((line, index) => {
    const fields = parseCsvLine(line);
    const date = parseNubankDate(fields[0] ?? "");
    const amountInfo = parseNubankAmount(fields[1] ?? "");
    const description = fields.slice(3).join(",").trim();

    if (!date || !amountInfo || !description) {
      throw new Error(`Linha ${index + 2} invalida.`);
    }

    return {
      amount: amountInfo.amount,
      category: IMPORTED_CATEGORY,
      date,
      description,
      type: amountInfo.type,
    };
  });
}

export async function createTransaction(
  _: TransactionFormState,
  formData: FormData
): Promise<TransactionFormState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const type = parseTransactionType(formData.get("type"));
  const amount = parseAmount(formData.get("amount"));
  const description = parseRequiredText(formData.get("description"));
  const category = parseRequiredText(formData.get("category"));
  const date = parseDate(formData.get("date"));

  if (!boardId || !type || !amount || !description || !category || !date) {
    return {
      error: "Preencha todos os campos com valores validos.",
      success: false,
    };
  }

  if (!VALID_TRANSACTION_TYPES.has(type)) {
    return {
      error: "Tipo de transacao invalido.",
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
    };
  }

  try {
    await createTransactionForCurrentUser({
      amount,
      boardId,
      category,
      date,
      description,
      type,
    });
  } catch {
    return {
      error: "Nao foi possivel salvar a transacao.",
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    success: true,
  };
}

export async function createShareLink(
  _: CreateShareLinkState,
  formData: FormData
): Promise<CreateShareLinkState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const origin = getSafeOrigin(formData.get("origin"));

  if (!boardId || !origin) {
    return {
      error: "Nao foi possivel gerar o link.",
      success: false,
      url: null,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
      url: null,
    };
  }

  const token = createShareToken();

  try {
    await createBoardShareLinkForCurrentUser({
      boardId,
      token,
    });
  } catch {
    return {
      error: "Nao foi possivel salvar o link.",
      success: false,
      url: null,
    };
  }

  return {
    error: null,
    success: true,
    url: `${origin}/share/${token}`,
  };
}

export async function updateTransaction(
  _: TransactionFormState,
  formData: FormData
): Promise<TransactionFormState> {
  const transactionId = parseRequiredText(formData.get("transactionId"));
  const boardId = parseRequiredText(formData.get("boardId"));
  const type = parseTransactionType(formData.get("type"));
  const amount = parseAmount(formData.get("amount"));
  const description = parseRequiredText(formData.get("description"));
  const category = parseRequiredText(formData.get("category"));
  const date = parseDate(formData.get("date"));

  if (
    !transactionId ||
    !boardId ||
    !type ||
    !amount ||
    !description ||
    !category ||
    !date
  ) {
    return {
      error: "Preencha todos os campos com valores validos.",
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
    };
  }

  try {
    await updateTransactionForCurrentUser({
      amount,
      boardId,
      category,
      date,
      description,
      transactionId,
      type,
    });
  } catch {
    return {
      error: "Nao foi possivel atualizar a transacao.",
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    success: true,
  };
}

export async function updateTransactionDescription(
  _: UpdateTransactionDescriptionState,
  formData: FormData
): Promise<UpdateTransactionDescriptionState> {
  const transactionId = parseRequiredText(formData.get("transactionId"));
  const boardId = parseRequiredText(formData.get("boardId"));
  const description = parseRequiredText(formData.get("description"));

  if (!transactionId || !boardId || !description || description.length > 120) {
    return {
      error: "Descricao invalida.",
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
    };
  }

  try {
    await updateTransactionDescriptionForCurrentUser({
      boardId,
      description,
      transactionId,
    });
  } catch {
    return {
      error: "Nao foi possivel atualizar a descricao.",
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    success: true,
  };
}

export async function updateTransactionsCategory(
  _: BulkUpdateTransactionCategoryState,
  formData: FormData
): Promise<BulkUpdateTransactionCategoryState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const category = parseRequiredText(formData.get("category"));
  const transactionIds = Array.from(
    new Set(
      formData
        .getAll("transactionIds")
        .map((value) => value.toString().trim())
        .filter((value) => value.length > 0)
    )
  );

  if (!boardId || !category || category.length > 80 || transactionIds.length === 0) {
    return {
      error: "Selecione transacoes e uma categoria valida.",
      success: false,
      updatedCount: 0,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
      updatedCount: 0,
    };
  }

  try {
    const updatedTransactions = await updateTransactionsCategoryForCurrentUser({
      boardId,
      category,
      transactionIds,
    });

    revalidatePath(`/board/${boardId}`);

    return {
      error: null,
      success: true,
      updatedCount: updatedTransactions.length,
    };
  } catch {
    return {
      error: "Nao foi possivel atualizar as categorias.",
      success: false,
      updatedCount: 0,
    };
  }
}

export async function deleteTransaction(
  _: DeleteTransactionState,
  formData: FormData
): Promise<DeleteTransactionState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const transactionId = parseRequiredText(formData.get("transactionId"));

  if (!boardId || !transactionId) {
    return {
      error: "Transacao invalida.",
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
    };
  }

  try {
    await deleteTransactionForCurrentUser({ boardId, transactionId });
  } catch {
    return {
      error: "Nao foi possivel remover a transacao.",
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    success: true,
  };
}

export async function importNubankCsv(
  _: ImportNubankCsvState,
  formData: FormData
): Promise<ImportNubankCsvState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const file = formData.get("csvFile");

  if (!boardId || !file || typeof file === "string" || typeof file.text !== "function") {
    return {
      error: "Selecione um arquivo CSV valido.",
      importedCount: 0,
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      importedCount: 0,
      success: false,
    };
  }

  let transactions: BulkCreateTransactionInput[];

  try {
    transactions = parseNubankCsv(await file.text());
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "CSV invalido.",
      importedCount: 0,
      success: false,
    };
  }

  try {
    await createTransactionsForCurrentUser({
      boardId,
      transactions,
    });
  } catch {
    return {
      error: "Nao foi possivel importar o CSV.",
      importedCount: 0,
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    importedCount: transactions.length,
    success: true,
  };
}

export async function createFinancialReserve(
  _: FinancialReserveFormState,
  formData: FormData
): Promise<FinancialReserveFormState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const name = parseRequiredText(formData.get("name"));
  const amount = parseAmount(formData.get("amount"));

  if (!boardId || !name || !amount) {
    return {
      error: "Preencha nome e valor com dados validos.",
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
    };
  }

  try {
    await createFinancialReserveForCurrentUser({ amount, boardId, name });
  } catch {
    return {
      error: "Nao foi possivel salvar a reserva.",
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    success: true,
  };
}

export async function updateFinancialReserve(
  _: FinancialReserveFormState,
  formData: FormData
): Promise<FinancialReserveFormState> {
  const reserveId = parseRequiredText(formData.get("reserveId"));
  const boardId = parseRequiredText(formData.get("boardId"));
  const name = parseRequiredText(formData.get("name"));
  const amount = parseAmount(formData.get("amount"));

  if (!reserveId || !boardId || !name || !amount) {
    return {
      error: "Preencha nome e valor com dados validos.",
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
    };
  }

  try {
    await updateFinancialReserveForCurrentUser({
      amount,
      boardId,
      name,
      reserveId,
    });
  } catch {
    return {
      error: "Nao foi possivel atualizar a reserva.",
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    success: true,
  };
}

export async function deleteFinancialReserve(
  _: DeleteFinancialReserveState,
  formData: FormData
): Promise<DeleteFinancialReserveState> {
  const boardId = parseRequiredText(formData.get("boardId"));
  const reserveId = parseRequiredText(formData.get("reserveId"));

  if (!boardId || !reserveId) {
    return {
      error: "Reserva invalida.",
      success: false,
    };
  }

  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    return {
      error: "Board inacessivel.",
      success: false,
    };
  }

  try {
    await deleteFinancialReserveForCurrentUser({ boardId, reserveId });
  } catch {
    return {
      error: "Nao foi possivel remover a reserva.",
      success: false,
    };
  }

  revalidatePath(`/board/${boardId}`);

  return {
    error: null,
    success: true,
  };
}
