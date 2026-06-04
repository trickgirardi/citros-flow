import { TransactionListPanel } from "@/components/panels/transaction-list-panel";
import type { TransactionGroup } from "@/components/panels/dashboard-data";

type EntradasPanelProps = {
  groups: TransactionGroup[];
  total: number;
};

export function EntradasPanel({ groups, total }: EntradasPanelProps) {
  return (
    <TransactionListPanel
      emptyLabel="Sem entradas neste board."
      groups={groups}
      title="Entradas"
      tone="entrada"
      total={total}
    />
  );
}
