"use client";

import { useActionState } from "react";
import Link from "next/link";

import {
  requestPasswordResetAction,
  type ForgotPasswordFormState,
} from "@/app/(auth)/forgot-password/actions";
import { Button } from "@/components/ui/button";

const INITIAL_STATE: ForgotPasswordFormState = {
  error: null,
  success: false,
};

export function ForgotPasswordForm() {
  const [state, formAction, isPending] = useActionState(
    async (currentState: ForgotPasswordFormState, formData: FormData) => {
      formData.set("origin", window.location.origin);

      return requestPasswordResetAction(currentState, formData);
    },
    INITIAL_STATE
  );

  if (state.success) {
    return (
      <div className="space-y-4">
        <p className="text-sm text-muted-foreground">
          Se houver uma conta com esse e-mail, enviamos um link para redefinir a senha.
          Confira sua caixa de entrada.
        </p>
        <Link href="/login" className="text-sm underline underline-offset-4">
          Voltar para o login
        </Link>
      </div>
    );
  }

  return (
    <form action={formAction} className="space-y-4">
      <div className="space-y-1.5">
        <label htmlFor="email" className="block text-sm font-medium">
          E-mail
        </label>
        <input
          id="email"
          name="email"
          type="email"
          required
          autoComplete="email"
          className="h-9 w-full rounded-md border bg-background px-3 text-sm"
          placeholder="voce@exemplo.com"
        />
      </div>

      {state.error ? <p className="text-sm text-destructive">{state.error}</p> : null}

      <Button type="submit" className="h-9 w-full" disabled={isPending}>
        {isPending ? "Enviando..." : "Enviar link de recuperacao"}
      </Button>

      <Link
        href="/login"
        className="block text-center text-sm text-muted-foreground underline underline-offset-4"
      >
        Voltar para o login
      </Link>
    </form>
  );
}
