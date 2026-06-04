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

export async function listTransactionsByBoard(boardId: string) {
  await requireCurrentUser();

  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("transactions")
    .select("*")
    .eq("board_id", boardId)
    .order("date", { ascending: false });

  if (error) {
    throw error;
  }

  return data;
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
