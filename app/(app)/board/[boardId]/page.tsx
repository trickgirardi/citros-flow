import { notFound } from "next/navigation";

import {
  getMonthScope,
  getSingleSearchParam,
} from "@/components/board/month-scope";
import { EntradasPanel } from "@/components/panels/EntradasPanel";
import { FechamentoPanel } from "@/components/panels/FechamentoPanel";
import { SaidasPanel } from "@/components/panels/SaidasPanel";
import {
  filterTransactionsByType,
  groupTransactionsByCategory,
  sumTransactionBalance,
  sumTransactions,
} from "@/components/panels/dashboard-data";
import {
  canMutateBoardForCurrentUser,
  getBoardForCurrentUser,
} from "@/lib/supabase/queries/boards";
import {
  listTransactionsBeforeDate,
  listTransactionsByBoard,
  listTransactionSuggestionsByBoard,
} from "@/lib/supabase/queries/transactions";

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
  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    notFound();
  }

  const [transactions, previousTransactions, canMutate, suggestions] = await Promise.all([
    listTransactionsByBoard({
      boardId: board.id,
      endDate: monthScope.endDate,
      startDate: monthScope.startDate,
    }),
    listTransactionsBeforeDate({
      beforeDate: monthScope.startDate,
      boardId: board.id,
    }),
    canMutateBoardForCurrentUser(board.id),
    listTransactionSuggestionsByBoard(board.id),
  ]);
  const entradas = filterTransactionsByType(transactions, "entrada");
  const saidas = filterTransactionsByType(transactions, "saida");
  const entradasTotal = sumTransactions(entradas);
  const saidasTotal = sumTransactions(saidas);
  const previousBalance = sumTransactionBalance(previousTransactions);

  return (
    <div className="grid h-full min-h-0 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] md:grid-rows-1">
      <EntradasPanel
        boardId={board.id}
        canMutate={canMutate}
        categorySuggestions={suggestions.categories}
        descriptionSuggestions={suggestions.descriptions}
        groups={groupTransactionsByCategory(entradas)}
        total={entradasTotal}
      />
      <SaidasPanel
        boardId={board.id}
        canMutate={canMutate}
        categorySuggestions={suggestions.categories}
        descriptionSuggestions={suggestions.descriptions}
        groups={groupTransactionsByCategory(saidas)}
        total={saidasTotal}
      />
      <FechamentoPanel
        balance={previousBalance + entradasTotal - saidasTotal}
        boardId={board.id}
        canMutate={canMutate}
        categorySuggestions={suggestions.categories}
        descriptionSuggestions={suggestions.descriptions}
        entradasTotal={entradasTotal}
        previousBalance={previousBalance}
        saidasTotal={saidasTotal}
        transactionCount={transactions.length}
      />
    </div>
  );
}
