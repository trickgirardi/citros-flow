import { notFound } from "next/navigation";

import { MonthNavigator } from "@/components/board/MonthNavigator";
import {
  getCurrentMonthKey,
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

  const [transactions, previousTransactions, canMutate] = await Promise.all([
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
  ]);
  const entradas = filterTransactionsByType(transactions, "entrada");
  const saidas = filterTransactionsByType(transactions, "saida");
  const entradasTotal = sumTransactions(entradas);
  const saidasTotal = sumTransactions(saidas);
  const previousBalance = sumTransactionBalance(previousTransactions);

  return (
    <section className="flex h-full min-h-0 flex-col overflow-hidden bg-muted/30 p-3 sm:p-4">
      <div className="mb-3 flex shrink-0 flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Board atual
          </p>
          <h1 className="truncate text-lg font-semibold sm:text-xl">{board.name}</h1>
          <p className="text-xs text-muted-foreground">
            {transactions.length} registros em {monthScope.label}
          </p>
        </div>
        <MonthNavigator
          boardId={board.id}
          currentMonthKey={getCurrentMonthKey()}
          label={monthScope.label}
          nextMonthKey={monthScope.nextMonthKey}
          previousMonthKey={monthScope.previousMonthKey}
        />
      </div>

      <div className="grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] md:grid-rows-1">
        <EntradasPanel
          boardId={board.id}
          canMutate={canMutate}
          groups={groupTransactionsByCategory(entradas)}
          total={entradasTotal}
        />
        <SaidasPanel
          boardId={board.id}
          canMutate={canMutate}
          groups={groupTransactionsByCategory(saidas)}
          total={saidasTotal}
        />
        <FechamentoPanel
          balance={previousBalance + entradasTotal - saidasTotal}
          boardId={board.id}
          canMutate={canMutate}
          entradasTotal={entradasTotal}
          previousBalance={previousBalance}
          saidasTotal={saidasTotal}
          transactionCount={transactions.length}
        />
      </div>
    </section>
  );
}
