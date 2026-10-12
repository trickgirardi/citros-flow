import { TransactionListPanel } from "@/components/panels/transaction-list-panel";
import type { TransactionGroup } from "@/components/panels/dashboard-data";

type EntradasPanelProps = {
  boardId: string;
  canMutate: boolean;
  categorySuggestions: string[];
  descriptionSuggestions: string[];
  groups: TransactionGroup[];
  total: number;
};

export function EntradasPanel({
  boardId,
  canMutate,
  categorySuggestions,
  descriptionSuggestions,
  groups,
  total,
}: EntradasPanelProps) {
  return (
    <TransactionListPanel
      boardId={boardId}
      canMutate={canMutate}
      categorySuggestions={categorySuggestions}
      descriptionSuggestions={descriptionSuggestions}
      emptyLabel="Sem entradas neste board."
      groups={groups}
      title="Entradas"
      tone="entrada"
      total={total}
    />
  );
}
