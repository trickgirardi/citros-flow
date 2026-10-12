import { TransactionListPanel } from "@/components/panels/transaction-list-panel";
import type { TransactionGroup } from "@/components/panels/dashboard-data";

type SaidasPanelProps = {
  boardId: string;
  canMutate: boolean;
  categorySuggestions: string[];
  descriptionSuggestions: string[];
  groups: TransactionGroup[];
  total: number;
};

export function SaidasPanel({
  boardId,
  canMutate,
  categorySuggestions,
  descriptionSuggestions,
  groups,
  total,
}: SaidasPanelProps) {
  return (
    <TransactionListPanel
      boardId={boardId}
      canMutate={canMutate}
      categorySuggestions={categorySuggestions}
      descriptionSuggestions={descriptionSuggestions}
      emptyLabel="Sem saidas neste board."
      groups={groups}
      title="Saidas"
      tone="saida"
      total={total}
    />
  );
}
