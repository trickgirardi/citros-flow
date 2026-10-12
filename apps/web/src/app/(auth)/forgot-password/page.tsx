import { redirect } from "next/navigation";

import { ForgotPasswordForm } from "@/components/auth/forgot-password-form";
import { getCurrentUser } from "@/lib/supabase/queries/auth";

export default async function ForgotPasswordPage() {
  const user = await getCurrentUser();

  if (user) {
    redirect("/board");
  }

  return (
    <main className="flex min-h-svh items-center justify-center p-6">
      <section className="w-full max-w-sm rounded-xl border bg-card p-6 shadow-sm">
        <div className="mb-5 space-y-1">
          <h1 className="text-2xl font-semibold">Recuperar senha</h1>
          <p className="text-sm text-muted-foreground">
            Informe seu e-mail para receber o link de redefinicao de senha.
          </p>
        </div>

        <ForgotPasswordForm />
      </section>
    </main>
  );
}
