import { createSupabaseServerClient } from "@/lib/supabase/server";

export async function getCurrentUser() {
  const supabase = await createSupabaseServerClient();
  const {
    data: { user },
  } = await supabase.auth.getUser();

  return user;
}

export async function requireCurrentUser() {
  const user = await getCurrentUser();

  if (!user) {
    throw new Error("Usuario nao autenticado.");
  }

  return user;
}

export async function signInWithEmailPassword(email: string, password: string) {
  const supabase = await createSupabaseServerClient();

  return supabase.auth.signInWithPassword({ email, password });
}

export async function signOutCurrentSession() {
  const supabase = await createSupabaseServerClient();

  return supabase.auth.signOut();
}
