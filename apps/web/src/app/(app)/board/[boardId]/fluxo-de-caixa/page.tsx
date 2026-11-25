import {
  getMonthScope,
  getSingleSearchParam,
} from "@/components/board/month-scope"
import { BoardCalendarControls } from "@/components/board/BoardCalendarControls"
import { ShareBoardButton } from "@/components/board/ShareBoardButton"
import { PageContent } from "@/components/layout/page-content"
import { PageHeader } from "@/components/layout/page-header"
import { EntradasPanel } from "@/components/panels/EntradasPanel"
import { FechamentoPanel } from "@/components/panels/FechamentoPanel"
import { SaidasPanel } from "@/components/panels/SaidasPanel"
import { createApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"
import { getBoard } from "@/lib/boards"

type BoardDashboardPageProps = {
  params: Promise<{
    boardId: string
  }>
  searchParams: Promise<{
    month?: string | string[]
  }>
}

export default async function BoardDashboardPage({
  params,
  searchParams,
}: BoardDashboardPageProps) {
  const [{ boardId }, { month }] = await Promise.all([params, searchParams])
  const monthScope = getMonthScope(getSingleSearchParam(month))
  const board = await getBoard(boardId)
  const api = await createApiClient()
  const path = { boardId: board.id }

  // Sugestões e a lista de reservas só servem a quem pode alterar (viewer vê o total no resumo).
  const [summary, suggestions, reserves] = await Promise.all([
    api
      .GET("/api/boards/{boardId}/months/{month}", {
        params: { path: { ...path, month: monthScope.monthKey } },
      })
      .then(readData),
    board.canMutate
      ? api
          .GET("/api/boards/{boardId}/suggestions", { params: { path } })
          .then(readData)
      : { categories: [], descriptions: [] },
    board.canMutate
      ? api
          .GET("/api/boards/{boardId}/reserves", { params: { path } })
          .then(readData)
      : [],
  ])

  return (
    <>
      <PageHeader
        scope={board.name}
        title="Fluxo de caixa"
        actions={
          <>
            <BoardCalendarControls boardId={board.id} />
            {/* Só quem pode alterar gera link público (viewer receberia 403). */}
            {board.canMutate ? (
              <>
                <span className="hidden @2xl/header:inline-flex">
                  <ShareBoardButton boardId={board.id} />
                </span>
                <span className="inline-flex @2xl/header:hidden">
                  <ShareBoardButton boardId={board.id} showLabel={false} />
                </span>
              </>
            ) : null}
          </>
        }
      />
      <PageContent>
        {/* O layout escolhe pela largura do CONTEÚDO (que a sidebar aberta comprime), não da janela:
            < 42rem empilhado · 42rem a 56rem entradas | saídas com o fechamento embaixo · ≥ 56rem
            três colunas. Sem rolagem da página. */}
        <div className="grid h-full min-h-0 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 @2xl:grid-cols-2 @2xl:grid-rows-[minmax(0,1fr)_auto] @4xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] @4xl:grid-rows-1 @2xl:[&>aside]:col-span-2 @4xl:[&>aside]:col-span-1">
          <EntradasPanel
            boardId={board.id}
            canMutate={board.canMutate}
            categorySuggestions={suggestions.categories}
            descriptionSuggestions={suggestions.descriptions}
            groups={summary.entradas}
            total={summary.entradasTotal}
          />
          <SaidasPanel
            boardId={board.id}
            canMutate={board.canMutate}
            categorySuggestions={suggestions.categories}
            descriptionSuggestions={suggestions.descriptions}
            groups={summary.saidas}
            total={summary.saidasTotal}
          />
          <FechamentoPanel
            balance={summary.balance}
            boardName={board.name}
            boardId={board.id}
            canMutate={board.canMutate}
            categorySuggestions={suggestions.categories}
            descriptionSuggestions={suggestions.descriptions}
            entradasTotal={summary.entradasTotal}
            monthLabel={monthScope.label}
            periodEnd={monthScope.lastDate}
            periodStart={monthScope.startDate}
            previousBalance={summary.previousBalance}
            reserves={reserves}
            reservasTotal={summary.reservesTotal}
            saidasTotal={summary.saidasTotal}
            transactionCount={summary.transactionCount}
          />
        </div>
      </PageContent>
    </>
  )
}
