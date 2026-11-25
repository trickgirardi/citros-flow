import {
  getCurrentMonthKey,
  getMonthScope,
} from "@/components/board/month-scope"
import { CashChart } from "@/components/dashboard/CashChart"
import { DashboardSection } from "@/components/dashboard/DashboardSection"
import { StatCard } from "@/components/dashboard/StatCard"
import { StockSummary } from "@/components/dashboard/StockSummary"
import { TaskSummary } from "@/components/dashboard/TaskSummary"
import { PageContent } from "@/components/layout/page-content"
import { PageHeader } from "@/components/layout/page-header"
import { createApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"
import { getBoard } from "@/lib/boards"
import { formatMoney, toCents } from "@/lib/money"

export default async function DashboardPage({
  params,
}: {
  params: Promise<{ boardId: string }>
}) {
  const { boardId } = await params
  const board = await getBoard(boardId)
  const month = getMonthScope(getCurrentMonthKey())
  const api = await createApiClient()
  // Os totais vêm calculados da API; aqui só se formata.
  const dashboard = readData(
    await api.GET("/api/boards/{boardId}/dashboard", {
      params: { path: { boardId: board.id }, query: { month: month.monthKey } },
    })
  )
  const { cash } = dashboard

  return (
    <>
      <PageHeader scope={board.name} title="Dashboard" />
      {/* A tela não rola: o dashboard rola dentro da área de conteúdo. */}
      <PageContent className="overflow-y-auto">
        <div className="mx-auto grid max-w-6xl gap-4">
          <p className="text-base text-muted-foreground">
            Resumo de {month.label}.
          </p>

          <DashboardSection
            href={`/board/${board.id}/fluxo-de-caixa`}
            linkLabel="Ver fluxo de caixa"
            title={`Caixa de ${month.label}`}
          >
            <div className="grid grid-cols-1 gap-2 @md:grid-cols-2 @5xl:grid-cols-4">
              <StatCard
                label="Saldo final"
                tone={toCents(cash.balance) >= 0 ? "positive" : "negative"}
                value={formatMoney(cash.balance)}
                detail={`Saldo anterior ${formatMoney(cash.previousBalance)}`}
              />
              <StatCard
                label="Entradas"
                value={formatMoney(cash.entradasTotal)}
                detail={`${cash.transactionCount} ${cash.transactionCount === 1 ? "transação" : "transações"} no mês`}
              />
              <StatCard label="Saídas" value={formatMoney(cash.saidasTotal)} />
              <StatCard
                label="Reservas"
                value={formatMoney(cash.reservesTotal)}
                detail="Informativo, fora do saldo"
              />
            </div>
            <CashChart
              currentMonth={month.monthKey}
              history={dashboard.history}
            />
          </DashboardSection>

          <div className="grid gap-4 @4xl:grid-cols-2">
            <TaskSummary boardId={board.id} tasks={dashboard.tasks} />
            <StockSummary boardId={board.id} stock={dashboard.stock} />
          </div>
        </div>
      </PageContent>
    </>
  )
}
