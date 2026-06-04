import { TransactionListPanel } from "@/components/panels/transaction-list-panel";
import type { TransactionGroup } from "@/components/panels/dashboard-data";

type EntradasPanelProps = {
  boardId: string;
  canMutate: boolean;
  groups: TransactionGroup[];
  total: number;
};

export function EntradasPanel({
  boardId,
  canMutate,
  groups,
  total,
}: EntradasPanelProps) {
  return (
    <TransactionListPanel
      boardId={boardId}
      canMutate={canMutate}
      emptyLabel="Sem entradas neste board."
      groups={groups}
      title="Entradas"
      tone="entrada"
      total={total}
    />
  );
}
