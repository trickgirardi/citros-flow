"use server";

import { redirect } from "next/navigation";

import { signInWithEmailPassword } from "@/lib/supabase/queries/auth";

export type LoginFormState = {
  error: string | null;
};

function getSafeNextPath(formData: FormData) {
  const next = formData.get("next")?.toString();

  if (!next || !next.startsWith("/") || next.startsWith("//")) {
    return "/board";
  }

  return next;
}

export async function login(_: LoginFormState, formData: FormData): Promise<LoginFormState> {
  const email = formData.get("email")?.toString().trim();
  const password = formData.get("password")?.toString();
  const nextPath = getSafeNextPath(formData);

  if (!email || !password) {
    return { error: "Preencha e-mail e senha." };
  }

  const { error } = await signInWithEmailPassword(email, password);

  if (error) {
    return { error: "Credenciais invalidas. Verifique e tente novamente." };
  }

  redirect(nextPath);
}
