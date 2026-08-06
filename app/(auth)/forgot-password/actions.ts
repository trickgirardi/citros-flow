"use server";

import { requestPasswordReset } from "@/lib/supabase/queries/auth";

export type ForgotPasswordFormState = {
  error: string | null;
  success: boolean;
};

const GENERIC_ERROR = "Nao foi possivel processar o pedido. Tente novamente.";

function getSafeOrigin(value: FormDataEntryValue | null) {
  const origin = value?.toString();

  if (!origin || !/^https?:\/\/[^/]+$/.test(origin)) {
    return null;
  }

  return origin;
}

export async function requestPasswordResetAction(
  _: ForgotPasswordFormState,
  formData: FormData
): Promise<ForgotPasswordFormState> {
  const email = formData.get("email")?.toString().trim();
  const origin = getSafeOrigin(formData.get("origin"));

  if (!email || !origin) {
    return { error: GENERIC_ERROR, success: false };
  }

  try {
    await requestPasswordReset(email, `${origin}/auth/confirm?next=/reset-password`);
  } catch {
    // Nao revela se o e-mail existe; segue para a mensagem generica de sucesso.
  }

  return { error: null, success: true };
}
