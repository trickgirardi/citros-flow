"use server";

import { redirect } from "next/navigation";

import {
  requireCurrentUser,
  signOutCurrentSession,
  updateCurrentUserPassword,
} from "@/lib/supabase/queries/auth";

export type ResetPasswordFormState = {
  error: string | null;
};

const MIN_PASSWORD_LENGTH = 6;

export async function updatePassword(
  _: ResetPasswordFormState,
  formData: FormData
): Promise<ResetPasswordFormState> {
  const password = formData.get("password")?.toString();
  const confirmPassword = formData.get("confirmPassword")?.toString();

  if (!password || password.length < MIN_PASSWORD_LENGTH) {
    return { error: `A senha deve ter no minimo ${MIN_PASSWORD_LENGTH} caracteres.` };
  }

  if (password !== confirmPassword) {
    return { error: "As senhas nao coincidem." };
  }

  try {
    await requireCurrentUser();
  } catch {
    return { error: "Link invalido ou expirado. Solicite uma nova redefinicao." };
  }

  const { error } = await updateCurrentUserPassword(password);

  if (error) {
    return { error: "Nao foi possivel atualizar a senha. Tente novamente." };
  }

  await signOutCurrentSession();

  redirect("/login?reset=success");
}
