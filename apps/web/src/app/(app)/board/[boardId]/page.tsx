import {
  getMonthScope,
  getSingleSearchParam,
} from "@/components/board/month-scope";
import { EntradasPanel } from "@/components/panels/EntradasPanel";
import { FechamentoPanel } from "@/components/panels/FechamentoPanel";
import { SaidasPanel } from "@/components/panels/SaidasPanel";
import { createApiClient } from "@/lib/api-client";
import { readData } from "@/lib/api-result";
import { getBoard } from "@/lib/boards";

type BoardDashboardPageProps = {
  params: Promise<{
    boardId: string;
  }>;
  searchParams: Promise<{
    month?: string | string[];
  }>;
};

export default async function BoardDashboardPage({
  params,
  searchParams,
}: BoardDashboardPageProps) {
  const [{ boardId }, { month }] = await Promise.all([params, searchParams]);
  const monthScope = getMonthScope(getSingleSearchParam(month));
  const board = await getBoard(boardId);
  const api = await createApiClient();
  const path = { boardId: board.id };

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
  ]);

  return (
    <div className="grid h-full min-h-0 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] md:grid-rows-1">
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
        entradaGroups={summary.entradas}
        entradasTotal={summary.entradasTotal}
        monthLabel={monthScope.label}
        previousBalance={summary.previousBalance}
        reserves={reserves}
        reservasTotal={summary.reservesTotal}
        saidaGroups={summary.saidas}
        saidasTotal={summary.saidasTotal}
        transactionCount={summary.transactionCount}
      />
    </div>
  );
}
