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

export async function canMutateBoardForCurrentUser(boardId: string) {
  const user = await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("user_roles")
    .select("board_id,role")
    .eq("user_id", user.id);

  if (error) {
    throw error;
  }

  return data.some(
    (role) =>
      (role.role === "admin" || role.role === "tesoureiro") &&
      (role.board_id === boardId || role.board_id === null)
  );
}
