import { redirect } from "next/navigation"

import { LoginForm } from "@/components/auth/login-form"
import { getCurrentUser } from "@/lib/supabase/queries/auth"

type LoginPageProps = {
  searchParams: Promise<{
    next?: string | string[]
    reset?: string | string[]
  }>
}

function getNextPath(next: string | string[] | undefined) {
  const value = Array.isArray(next) ? next[0] : next

  if (!value || !value.startsWith("/") || value.startsWith("//")) {
    return "/board"
  }

  return value
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const { next, reset } = await searchParams
  const nextPath = getNextPath(next)
  const user = await getCurrentUser()
  const resetSucceeded = (Array.isArray(reset) ? reset[0] : reset) === "success"

  if (user) {
    redirect(nextPath)
  }

  return (
    <main className="flex min-h-svh items-center justify-center p-6">
      <section className="w-full max-w-sm rounded-xl border bg-card p-6 shadow-sm">
        <div className="mb-5 space-y-1">
          <h1 className="text-2xl font-semibold">Gnosis - Fluxo de Caixa</h1>
          <p className="text-sm text-muted-foreground">
            Acesse com seu e-mail e senha.
          </p>
        </div>

        {resetSucceeded ? (
          <p className="mb-4 text-sm text-emerald-600 dark:text-emerald-400">
            Senha atualizada. Faca login com sua nova senha.
          </p>
        ) : null}

        <LoginForm nextPath={nextPath} />
      </section>
    </main>
  )
}
