"use client";

import { useActionState } from "react";
import Link from "next/link";

import { login, type LoginFormState } from "@/app/(auth)/login/actions";
import { Button } from "@/components/ui/button";

const INITIAL_STATE: LoginFormState = {
  error: null,
};

type LoginFormProps = {
  nextPath: string;
};

export function LoginForm({ nextPath }: LoginFormProps) {
  const [state, formAction, isPending] = useActionState(login, INITIAL_STATE);

  return (
    <form action={formAction} className="space-y-4">
      <input type="hidden" name="next" value={nextPath} />

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

      <div className="space-y-1.5">
        <label htmlFor="password" className="block text-sm font-medium">
          Senha
        </label>
        <input
          id="password"
          name="password"
          type="password"
          required
          autoComplete="current-password"
          className="h-9 w-full rounded-md border bg-background px-3 text-sm"
        />
      </div>

      {state.error ? <p className="text-sm text-destructive">{state.error}</p> : null}

      <Button type="submit" className="h-9 w-full" disabled={isPending}>
        {isPending ? "Entrando..." : "Entrar"}
      </Button>

      <Link
        href="/forgot-password"
        className="block text-center text-sm text-muted-foreground underline underline-offset-4"
      >
        Esqueceu sua senha?
      </Link>
    </form>
  );
}
