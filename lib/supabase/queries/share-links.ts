import { hashShareToken } from "@/lib/share-token";
import { createSupabaseServerClient } from "@/lib/supabase/server";
import { createSupabaseServiceRoleClient } from "@/lib/supabase/service";
import { requireCurrentUser } from "@/lib/supabase/queries/auth";

export async function createBoardShareLinkForCurrentUser({
  boardId,
  token,
}: {
  boardId: string;
  token: string;
}) {
  const user = await requireCurrentUser();
  const supabase = await createSupabaseServerClient();

  const { data, error } = await supabase
    .from("board_share_links")
    .insert({
      board_id: boardId,
      created_by: user.id,
      token_hash: hashShareToken(token),
    })
    .select("id")
    .single();

  if (error) {
    throw error;
  }

  return data;
}

export async function getBoardByShareToken(token: string) {
  const supabase = createSupabaseServiceRoleClient();
  const tokenHash = hashShareToken(token);

  const { data, error } = await supabase
    .from("board_share_links")
    .select("board_id,expires_at,revoked_at,boards(id,name)")
    .eq("token_hash", tokenHash)
    .maybeSingle();

  if (error) {
    throw error;
  }

  if (!data || data.revoked_at) {
    return null;
  }

  if (data.expires_at && new Date(data.expires_at).getTime() <= Date.now()) {
    return null;
  }

  const board = Array.isArray(data.boards) ? data.boards[0] : data.boards;

  if (!board) {
    return null;
  }

  return {
    id: board.id,
    name: board.name,
  };
}
