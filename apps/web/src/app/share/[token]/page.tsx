import Link from "next/link"
import { notFound } from "next/navigation"

import {
  getMonthScope,
  getSingleSearchParam,
} from "@/components/board/month-scope"
import { EntradasPanel } from "@/components/panels/EntradasPanel"
import { FechamentoPanel } from "@/components/panels/FechamentoPanel"
import { SaidasPanel } from "@/components/panels/SaidasPanel"
import {
  filterTransactionsByType,
  groupTransactionsByCategory,
  sumFinancialReserves,
  sumTransactionBalance,
  sumTransactions,
} from "@/components/panels/dashboard-data"
import { listFinancialReservesByBoardWithServiceRole } from "@/lib/supabase/queries/financial-reserves"
import { getBoardByShareToken } from "@/lib/supabase/queries/share-links"
import {
  listTransactionsBeforeDateWithServiceRole,
  listTransactionsByBoardWithServiceRole,
} from "@/lib/supabase/queries/transactions"
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
  const board = await getBoardByShareToken(token)

  if (!board) {
    notFound()
  }

  const monthScope = getMonthScope(getSingleSearchParam(month))
  const [transactions, previousTransactions, reserves] = await Promise.all([
    listTransactionsByBoardWithServiceRole({
      boardId: board.id,
      endDate: monthScope.endDate,
      startDate: monthScope.startDate,
    }),
    listTransactionsBeforeDateWithServiceRole({
      beforeDate: monthScope.startDate,
      boardId: board.id,
    }),
    listFinancialReservesByBoardWithServiceRole(board.id),
  ])
  const entradas = filterTransactionsByType(transactions, "entrada")
  const saidas = filterTransactionsByType(transactions, "saida")
  const entradaGroups = groupTransactionsByCategory(entradas)
  const saidaGroups = groupTransactionsByCategory(saidas)
  const entradasTotal = sumTransactions(entradas)
  const saidasTotal = sumTransactions(saidas)
  const previousBalance = sumTransactionBalance(previousTransactions)
  const reservasTotal = sumFinancialReserves(reserves)

  return (
    <main className="flex h-svh flex-col overflow-hidden bg-muted/30">
      <header className="flex shrink-0 flex-row justify-between gap-2 border-b bg-background px-3 py-2 sm:flex-row sm:items-center sm:justify-between sm:px-4">
        <div className="min-w-0">
          <p className="text-xs font-medium text-muted-foreground">
            Fluxo de caixa
          </p>
          <h1 className="truncate text-sm font-semibold">{board.name}</h1>
        </div>
        <ShareMonthNavigator
          currentMonthKey={monthScope.monthKey}
          label={monthScope.label}
          nextMonthKey={monthScope.nextMonthKey}
          previousMonthKey={monthScope.previousMonthKey}
          token={token}
        />
      </header>

      <div className="grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 overflow-hidden p-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] md:grid-rows-1 md:p-4">
        <EntradasPanel
          boardId={board.id}
          canMutate={false}
          categorySuggestions={[]}
          descriptionSuggestions={[]}
          groups={entradaGroups}
          total={entradasTotal}
        />
        <SaidasPanel
          boardId={board.id}
          canMutate={false}
          categorySuggestions={[]}
          descriptionSuggestions={[]}
          groups={saidaGroups}
          total={saidasTotal}
        />
        <FechamentoPanel
          balance={previousBalance + entradasTotal - saidasTotal}
          boardName={board.name}
          boardId={board.id}
          canMutate={false}
          categorySuggestions={[]}
          descriptionSuggestions={[]}
          entradaCategories={entradaGroups.map((group) => ({
            category: group.category,
            total: group.total,
          }))}
          entradasTotal={entradasTotal}
          monthLabel={monthScope.label}
          previousBalance={previousBalance}
          reserves={[]}
          reservasTotal={reservasTotal}
          saidaCategories={saidaGroups.map((group) => ({
            category: group.category,
            total: group.total,
          }))}
          saidasTotal={saidasTotal}
          transactionCount={transactions.length}
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
