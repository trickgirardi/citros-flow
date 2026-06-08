import { createSupabaseServerClient } from "@/lib/supabase/server";
import { requireCurrentUser } from "@/lib/supabase/queries/auth";

export type CreateTransactionInput = {
  amount: number;
  boardId: string;
  category: string;
  date: string;
  description: string;
  type: "entrada" | "saida";
};

export type UpdateTransactionInput = CreateTransactionInput & {
  transactionId: string;
};

export type UpdateTransactionDescriptionInput = {
  boardId: string;
  description: string;
  transactionId: string;
};

export type BulkCreateTransactionInput = Omit<CreateTransactionInput, "boardId">;

type ListTransactionsByBoardInput = {
  boardId: string;
  endDate: string;
  startDate: string;
};

type ListTransactionsBeforeDateInput = {
  beforeDate: string;
  boardId: string;
};

export type TransactionSuggestions = {
  categories: string[];
  descriptions: string[];
};

export async function listTransactionsByBoard({
  boardId,
  endDate,
  startDate,
}: ListTransactionsByBoardInput) {
  await requireCurrentUser();

  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .select("*")
    .eq("board_id", boardId)
    .gte("date", startDate)
    .lt("date", endDate)
    .order("date", { ascending: false });

  if (error) {
    throw error;
  }

  return data;
}

export async function listTransactionsBeforeDate({
  beforeDate,
  boardId,
}: ListTransactionsBeforeDateInput) {
  await requireCurrentUser();

  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .select("amount,type")
    .eq("board_id", boardId)
    .lt("date", beforeDate);

  if (error) {
    throw error;
  }

  return data;
}

export async function listTransactionSuggestionsByBoard(
  boardId: string
): Promise<TransactionSuggestions> {
  await requireCurrentUser();

  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .select("category,description")
    .eq("board_id", boardId)
    .order("category", { ascending: true });

  if (error) {
    throw error;
  }

  return {
    categories: uniqueSorted(data.map((transaction) => transaction.category)),
    descriptions: uniqueSorted(data.map((transaction) => transaction.description)),
  };
}

function uniqueSorted(values: string[]) {
  return Array.from(
    new Set(
      values
        .map((value) => value.trim())
        .filter((value) => value.length > 0)
    )
  ).sort((first, second) => first.localeCompare(second, "pt-BR"));
}

export async function createTransactionForCurrentUser(input: CreateTransactionInput) {
  const user = await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .insert({
      amount: input.amount,
      board_id: input.boardId,
      category: input.category,
      created_by: user.id,
      date: input.date,
      description: input.description,
      type: input.type,
    })
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
}

export async function createTransactionsForCurrentUser({
  boardId,
  transactions,
}: {
  boardId: string;
  transactions: BulkCreateTransactionInput[];
}) {
  const user = await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .insert(
      transactions.map((transaction) => ({
        amount: transaction.amount,
        board_id: boardId,
        category: transaction.category,
        created_by: user.id,
        date: transaction.date,
        description: transaction.description,
        type: transaction.type,
      }))
    )
    .select("id");

  if (error) {
    throw error;
  }

  return data;
}

export async function updateTransactionForCurrentUser(input: UpdateTransactionInput) {
  await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .update({
      amount: input.amount,
      category: input.category,
      date: input.date,
      description: input.description,
      type: input.type,
    })
    .eq("id", input.transactionId)
    .eq("board_id", input.boardId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
}

export async function updateTransactionDescriptionForCurrentUser({
  boardId,
  description,
  transactionId,
}: UpdateTransactionDescriptionInput) {
  await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .update({ description })
    .eq("id", transactionId)
    .eq("board_id", boardId)
    .select("id")
    .single();

  if (error) {
    throw error;
  }

  return data;
}

export async function deleteTransactionForCurrentUser({
  boardId,
  transactionId,
}: {
  boardId: string;
  transactionId: string;
}) {
  await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { error } = await supabase
    .from("transactions")
    .delete()
    .eq("id", transactionId)
    .eq("board_id", boardId)
    .select("id")
    .single();

  if (error) {
    throw error;
  }
}
