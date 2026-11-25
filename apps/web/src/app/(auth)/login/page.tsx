import { redirect } from "next/navigation"

import { ThemeToggle } from "@/components/theme-provider"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { LogoAdaptive } from "@/components/ui/logo"
import { getCurrentUser } from "@/lib/auth"

type LoginPageProps = {
  searchParams: Promise<{
    error?: string | string[]
    next?: string | string[]
  }>
}

// Códigos de erro que voltam da API (not_allowed, unverified, google_failed, invalid_credentials,
// too_many_attempts) e do próprio front.
const ERROR_MESSAGES: Record<string, string> = {
  invalid_credentials: "E-mail ou senha incorretos.",
  too_many_attempts:
    "Muitas tentativas para este e-mail. Aguarde 15 minutos e tente novamente.",
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
    <main className="relative flex min-h-svh items-center justify-center p-6">
      <div className="absolute top-4 right-4">
        <ThemeToggle />
      </div>

      <section className="w-full max-w-sm rounded-panel border bg-card p-6 shadow-card sm:p-8">
        <div className="mb-6 flex justify-center">
          <LogoAdaptive />
        </div>

        <div className="mb-6 space-y-1 text-center">
          <h1 className="text-h1 text-2xl">Fluxo de Caixa</h1>
          <p className="text-base text-muted-foreground">
            Entre com e-mail e senha ou com a sua conta Google.
          </p>
        </div>

        {errorMessage ? (
          <p
            role="alert"
            className="mb-4 rounded-lg bg-card text-sm font-semibold text-destructive"
          >
            {errorMessage}
          </p>
        ) : null}

        {/* Form HTML comum (nao Server Action): o browser envia direto para a API, que cria a sessao e redireciona. */}
        <form
          method="post"
          action="/api/auth/password-login"
          className="grid gap-4"
        >
          <input type="hidden" name="returnUrl" value={nextPath} />
          <label className="grid gap-1.5 text-base font-semibold">
            E-mail
            <Input
              name="email"
              type="email"
              required
              autoComplete="username"
              className="font-normal"
            />
          </label>
          <label className="grid gap-1.5 text-base font-semibold">
            Senha
            <Input
              name="password"
              type="password"
              required
              autoComplete="current-password"
              className="font-normal"
            />
          </label>
          <Button type="submit" size="lg" className="w-full">
            Entrar
          </Button>
        </form>

        <div className="my-5 flex items-center gap-3 text-sm text-muted-foreground">
          <span className="h-px flex-1 bg-border" />
          ou
          <span className="h-px flex-1 bg-border" />
        </div>

        {/* <a> comum, nao <Link>: o login e um fluxo de redirects para o Google, sem prefetch nem navegacao do Next. */}
        <Button asChild variant="outline" size="lg" className="w-full">
          <a href={`/api/auth/login?returnUrl=${encodeURIComponent(nextPath)}`}>
            Entrar com Google
          </a>
        </Button>
      </section>
    </main>
  )
}
