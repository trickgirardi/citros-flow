import { createSupabaseServerClient } from "@/lib/supabase/server";
import { createSupabaseServiceRoleClient } from "@/lib/supabase/service";
import { requireCurrentUser } from "@/lib/supabase/queries/auth";

export type CreateFinancialReserveInput = {
  amount: number;
  boardId: string;
  name: string;
};

export type UpdateFinancialReserveInput = CreateFinancialReserveInput & {
  reserveId: string;
};

export async function listFinancialReservesByBoard(boardId: string) {
  await requireCurrentUser();

  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("financial_reserves")
    .select("*")
    .eq("board_id", boardId)
    .order("created_at", { ascending: true });

  if (error) {
    throw error;
  }

  return data;
}

export async function listFinancialReservesByBoardWithServiceRole(boardId: string) {
  const supabase = createSupabaseServiceRoleClient();

  const { data, error } = await supabase
    .from("financial_reserves")
    .select("*")
    .eq("board_id", boardId)
    .order("created_at", { ascending: true });

  if (error) {
    throw error;
  }

  return data;
}

export async function createFinancialReserveForCurrentUser(
  input: CreateFinancialReserveInput
) {
  const user = await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("financial_reserves")
    .insert({
      amount: input.amount,
      board_id: input.boardId,
      created_by: user.id,
      name: input.name,
    })
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
}

export async function updateFinancialReserveForCurrentUser(
  input: UpdateFinancialReserveInput
) {
  await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("financial_reserves")
    .update({
      amount: input.amount,
      name: input.name,
    })
    .eq("id", input.reserveId)
    .eq("board_id", input.boardId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
}

export async function deleteFinancialReserveForCurrentUser({
  boardId,
  reserveId,
}: {
  boardId: string;
  reserveId: string;
}) {
  await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { error } = await supabase
    .from("financial_reserves")
    .delete()
    .eq("id", reserveId)
    .eq("board_id", boardId)
    .select("id")
    .single();

  if (error) {
    throw error;
  }
}
