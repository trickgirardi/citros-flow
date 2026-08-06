"use client";

import { useActionState } from "react";

import { updatePassword, type ResetPasswordFormState } from "@/app/(auth)/reset-password/actions";
import { Button } from "@/components/ui/button";

const INITIAL_STATE: ResetPasswordFormState = {
  error: null,
};

export function ResetPasswordForm() {
  const [state, formAction, isPending] = useActionState(updatePassword, INITIAL_STATE);

  return (
    <form action={formAction} className="space-y-4">
      <div className="space-y-1.5">
        <label htmlFor="password" className="block text-sm font-medium">
          Nova senha
        </label>
        <input
          id="password"
          name="password"
          type="password"
          required
          autoComplete="new-password"
          className="h-9 w-full rounded-md border bg-background px-3 text-sm"
        />
      </div>

      <div className="space-y-1.5">
        <label htmlFor="confirmPassword" className="block text-sm font-medium">
          Confirmar nova senha
        </label>
        <input
          id="confirmPassword"
          name="confirmPassword"
          type="password"
          required
          autoComplete="new-password"
          className="h-9 w-full rounded-md border bg-background px-3 text-sm"
        />
      </div>

      {state.error ? <p className="text-sm text-destructive">{state.error}</p> : null}

      <Button type="submit" className="h-9 w-full" disabled={isPending}>
        {isPending ? "Salvando..." : "Salvar nova senha"}
      </Button>
    </form>
  );
}
