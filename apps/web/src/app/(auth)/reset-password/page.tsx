import Link from "next/link";

import { ResetPasswordForm } from "@/components/auth/reset-password-form";
import { getCurrentUser } from "@/lib/supabase/queries/auth";

export default async function ResetPasswordPage() {
  const user = await getCurrentUser();

  return (
    <main className="flex min-h-svh items-center justify-center p-6">
      <section className="w-full max-w-sm rounded-xl border bg-card p-6 shadow-sm">
        <div className="mb-5 space-y-1">
          <h1 className="text-2xl font-semibold">Definir nova senha</h1>
          {user ? (
            <p className="text-sm text-muted-foreground">
              Escolha uma nova senha para sua conta.
            </p>
          ) : null}
        </div>

        {user ? (
          <ResetPasswordForm />
        ) : (
          <div className="space-y-4">
            <p className="text-sm text-destructive">
              Este link de redefinicao e invalido, ja foi utilizado ou expirou.
            </p>
            <Link
              href="/forgot-password"
              className="text-sm underline underline-offset-4"
            >
              Solicitar um novo link
            </Link>
          </div>
        )}
      </section>
    </main>
  );
}
