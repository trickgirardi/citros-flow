import { redirect } from "next/navigation"

import { Button } from "@/components/ui/button"
import { getCurrentUser } from "@/lib/auth"

type LoginPageProps = {
  searchParams: Promise<{
    error?: string | string[]
    next?: string | string[]
  }>
}

// Códigos de erro que voltam da API (not_allowed, unverified, google_failed) e do próprio front.
const ERROR_MESSAGES: Record<string, string> = {
  not_allowed:
    "Este e-mail nao esta autorizado a acessar o sistema. Fale com o administrador.",
  unverified:
    "O Google informou que o e-mail desta conta nao esta verificado. Verifique-o e tente novamente.",
  google_failed: "Nao foi possivel entrar com o Google. Tente novamente.",
  session_expired: "Sua sessao expirou. Entre novamente.",
}

function getSingle(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value
}

function getNextPath(next: string | undefined) {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) {
    return "/board"
  }

  return next
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const params = await searchParams
  const nextPath = getNextPath(getSingle(params.next))
  const errorCode = getSingle(params.error)

  if (await getCurrentUser()) {
    redirect(nextPath)
  }

  const errorMessage = errorCode ? ERROR_MESSAGES[errorCode] : undefined

  return (
    <main className="flex min-h-svh items-center justify-center p-6">
      <section className="w-full max-w-sm rounded-xl border bg-card p-6 shadow-sm">
        <div className="mb-5 space-y-1">
          <h1 className="text-2xl font-semibold">Gnosis - Fluxo de Caixa</h1>
          <p className="text-sm text-muted-foreground">
            Acesse com a sua conta Google.
          </p>
        </div>

        {errorMessage ? (
          <p role="alert" className="mb-4 text-sm text-destructive">
            {errorMessage}
          </p>
        ) : null}

        {/* <a> comum, nao <Link>: o login e um fluxo de redirects para o Google, sem prefetch nem navegacao do Next. */}
        <Button asChild className="h-9 w-full">
          <a href={`/api/auth/login?returnUrl=${encodeURIComponent(nextPath)}`}>
            Entrar com Google
          </a>
        </Button>
      </section>
    </main>
  )
}
