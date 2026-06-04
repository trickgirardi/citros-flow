import { TransactionListPanel } from "@/components/panels/transaction-list-panel";
import type { TransactionGroup } from "@/components/panels/dashboard-data";

type SaidasPanelProps = {
  boardId: string;
  canMutate: boolean;
  groups: TransactionGroup[];
  total: number;
};

export function SaidasPanel({
  boardId,
  canMutate,
  groups,
  total,
}: SaidasPanelProps) {
  return (
    <TransactionListPanel
      boardId={boardId}
      canMutate={canMutate}
      emptyLabel="Sem saidas neste board."
      groups={groups}
      title="Saidas"
      tone="saida"
      total={total}
    />
  );
}
