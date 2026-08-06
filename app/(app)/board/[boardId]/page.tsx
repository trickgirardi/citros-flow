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
  sumFinancialReserves,
  sumTransactionBalance,
  sumTransactions,
} from "@/components/panels/dashboard-data";
import {
  canMutateBoardForCurrentUser,
  getBoardForCurrentUser,
} from "@/lib/supabase/queries/boards";
import { listFinancialReservesByBoard } from "@/lib/supabase/queries/financial-reserves";
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

  const [transactions, previousTransactions, canMutate, suggestions, reserves] =
    await Promise.all([
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
      listFinancialReservesByBoard(board.id),
    ]);
  const entradas = filterTransactionsByType(transactions, "entrada");
  const saidas = filterTransactionsByType(transactions, "saida");
  const entradaGroups = groupTransactionsByCategory(entradas);
  const saidaGroups = groupTransactionsByCategory(saidas);
  const entradasTotal = sumTransactions(entradas);
  const saidasTotal = sumTransactions(saidas);
  const previousBalance = sumTransactionBalance(previousTransactions);
  const reservasTotal = sumFinancialReserves(reserves);

  return (
    <div className="grid h-full min-h-0 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] md:grid-rows-1">
      <EntradasPanel
        boardId={board.id}
        canMutate={canMutate}
        categorySuggestions={suggestions.categories}
        descriptionSuggestions={suggestions.descriptions}
        groups={entradaGroups}
        total={entradasTotal}
      />
      <SaidasPanel
        boardId={board.id}
        canMutate={canMutate}
        categorySuggestions={suggestions.categories}
        descriptionSuggestions={suggestions.descriptions}
        groups={saidaGroups}
        total={saidasTotal}
      />
      <FechamentoPanel
        balance={previousBalance + entradasTotal - saidasTotal}
        boardName={board.name}
        boardId={board.id}
        canMutate={canMutate}
        categorySuggestions={suggestions.categories}
        descriptionSuggestions={suggestions.descriptions}
        entradaCategories={entradaGroups.map((group) => ({
          category: group.category,
          total: group.total,
        }))}
        entradasTotal={entradasTotal}
        monthLabel={monthScope.label}
        previousBalance={previousBalance}
        reserves={reserves}
        reservasTotal={reservasTotal}
        saidaCategories={saidaGroups.map((group) => ({
          category: group.category,
          total: group.total,
        }))}
        saidasTotal={saidasTotal}
        transactionCount={transactions.length}
      />
    </div>
  );
}
