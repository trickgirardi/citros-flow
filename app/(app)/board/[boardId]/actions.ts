"use server";

import { revalidatePath } from "next/cache";

import { getBoardForCurrentUser } from "@/lib/supabase/queries/boards";
import { createTransactionForCurrentUser } from "@/lib/supabase/queries/transactions";

export type TransactionFormState = {
  error: string | null;
  success: boolean;
};

const VALID_TRANSACTION_TYPES = new Set(["entrada", "saida"]);

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
