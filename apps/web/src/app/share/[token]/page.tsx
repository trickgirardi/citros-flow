import Link from "next/link"

import {
  getCurrentMonthKey,
  getMonthScope,
  getSingleSearchParam,
} from "@/components/board/month-scope"
import { EntradasPanel } from "@/components/panels/EntradasPanel"
import { FechamentoPanel } from "@/components/panels/FechamentoPanel"
import { SaidasPanel } from "@/components/panels/SaidasPanel"
import { createPublicApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"
import { ChevronLeft, ChevronRight } from "lucide-react"

type ShareBoardPageProps = {
  params: Promise<{
    token: string
  }>
  searchParams: Promise<{
    month?: string | string[]
  }>
}

export default async function ShareBoardPage({
  params,
  searchParams,
}: ShareBoardPageProps) {
  const [{ token }, { month }] = await Promise.all([params, searchParams])
  const monthScope = getMonthScope(getSingleSearchParam(month))
  const api = createPublicApiClient()
  // Link inexistente, revogado ou expirado: a API responde 404 e a página mostra o 404.
  const { boardName, summary } = readData(
    await api.GET("/api/share/{token}/months/{month}", {
      params: { path: { token, month: monthScope.monthKey } },
    })
  )

  // O link público não expõe o id do board: sem canMutate os painéis não usam boardId.
  return (
    <main className="flex h-svh flex-col overflow-hidden bg-muted/30">
      <header className="flex shrink-0 flex-row justify-between gap-2 border-b bg-background px-3 py-2 sm:flex-row sm:items-center sm:justify-between sm:px-4">
        <div className="min-w-0">
          <p className="text-xs font-medium text-muted-foreground">
            Fluxo de caixa
          </p>
          <h1 className="truncate text-sm font-semibold">{boardName}</h1>
        </div>
        <ShareMonthNavigator
          currentMonthKey={getCurrentMonthKey()}
          label={monthScope.label}
          nextMonthKey={monthScope.nextMonthKey}
          previousMonthKey={monthScope.previousMonthKey}
          token={token}
        />
      </header>

      <div className="grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 overflow-hidden p-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] md:grid-rows-1 md:p-4">
        <EntradasPanel
          boardId=""
          canMutate={false}
          categorySuggestions={[]}
          descriptionSuggestions={[]}
          groups={summary.entradas}
          total={summary.entradasTotal}
        />
        <SaidasPanel
          boardId=""
          canMutate={false}
          categorySuggestions={[]}
          descriptionSuggestions={[]}
          groups={summary.saidas}
          total={summary.saidasTotal}
        />
        <FechamentoPanel
          balance={summary.balance}
          boardName={boardName}
          boardId=""
          canMutate={false}
          categorySuggestions={[]}
          descriptionSuggestions={[]}
          entradaGroups={summary.entradas}
          entradasTotal={summary.entradasTotal}
          monthLabel={monthScope.label}
          previousBalance={summary.previousBalance}
          reserves={[]}
          reservasTotal={summary.reservesTotal}
          saidaGroups={summary.saidas}
          saidasTotal={summary.saidasTotal}
          transactionCount={summary.transactionCount}
        />
      </div>
    </main>
  )
}

function ShareMonthNavigator({
  currentMonthKey,
  label,
  nextMonthKey,
  previousMonthKey,
  token,
}: {
  currentMonthKey: string
  label: string
  nextMonthKey: string
  previousMonthKey: string
  token: string
}) {
  return (
    <nav
      className="flex items-center gap-1 text-xs"
      aria-label="Navegacao mensal publica"
    >
      <Link
        className="rounded-md border px-2 py-1"
        href={getShareMonthHref(token, previousMonthKey)}
      >
        <ChevronLeft size={16} />
      </Link>
      <span className="min-w-28 rounded-md border bg-muted px-2 py-1 text-center capitalize">
        {label}
      </span>
      <Link
        className="rounded-md border px-2 py-1"
        href={getShareMonthHref(token, nextMonthKey)}
      >
        <ChevronRight size={16} />
      </Link>
      <Link
        className="rounded-md border px-2 py-1"
        href={getShareMonthHref(token, currentMonthKey)}
      >
        Hoje
      </Link>
    </nav>
  )
}

function getShareMonthHref(token: string, monthKey: string) {
  return `/share/${token}?month=${monthKey}`
}
