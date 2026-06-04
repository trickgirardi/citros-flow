import { createSupabaseServerClient } from "@/lib/supabase/server";
import { requireCurrentUser } from "@/lib/supabase/queries/auth";

export async function listBoardsForCurrentUser() {
  await requireCurrentUser();

  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase.from("boards").select("*").order("name");

  if (error) {
    throw error;
  }

  return data;
}

export async function getBoardForCurrentUser(boardId: string) {
  await requireCurrentUser();

  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("boards")
    .select("*")
    .eq("id", boardId)
    .maybeSingle();

  if (error) {
    throw error;
  }

  return data;
}
