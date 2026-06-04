import { notFound } from "next/navigation";

import { EntradasPanel } from "@/components/panels/EntradasPanel";
import { FechamentoPanel } from "@/components/panels/FechamentoPanel";
import { SaidasPanel } from "@/components/panels/SaidasPanel";
import {
  filterTransactionsByType,
  groupTransactionsByCategory,
  sumTransactions,
} from "@/components/panels/dashboard-data";
import { getBoardForCurrentUser } from "@/lib/supabase/queries/boards";
import { listTransactionsByBoard } from "@/lib/supabase/queries/transactions";

type BoardDashboardPageProps = {
  params: Promise<{
    boardId: string;
  }>;
};

export default async function BoardDashboardPage({ params }: BoardDashboardPageProps) {
  const { boardId } = await params;
  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    notFound();
  }

  const transactions = await listTransactionsByBoard(board.id);
  const entradas = filterTransactionsByType(transactions, "entrada");
  const saidas = filterTransactionsByType(transactions, "saida");
  const entradasTotal = sumTransactions(entradas);
  const saidasTotal = sumTransactions(saidas);

  return (
    <section className="flex h-full min-h-0 flex-col overflow-hidden bg-muted/30 p-3 sm:p-4">
      <div className="mb-3 flex shrink-0 items-end justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Board atual
          </p>
          <h1 className="truncate text-lg font-semibold sm:text-xl">{board.name}</h1>
        </div>
        <p className="shrink-0 text-xs text-muted-foreground">{transactions.length} registros</p>
      </div>

      <div className="grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_20rem] md:grid-rows-1">
        <EntradasPanel
          groups={groupTransactionsByCategory(entradas)}
          total={entradasTotal}
        />
        <SaidasPanel groups={groupTransactionsByCategory(saidas)} total={saidasTotal} />
        <FechamentoPanel
          balance={entradasTotal - saidasTotal}
          entradasTotal={entradasTotal}
          saidasTotal={saidasTotal}
          transactionCount={transactions.length}
        />
      </div>
    </section>
  );
}
