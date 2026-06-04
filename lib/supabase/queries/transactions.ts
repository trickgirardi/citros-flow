import { createSupabaseServerClient } from "@/lib/supabase/server";
import { requireCurrentUser } from "@/lib/supabase/queries/auth";

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
