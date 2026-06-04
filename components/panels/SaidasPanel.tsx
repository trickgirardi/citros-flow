import { TransactionListPanel } from "@/components/panels/transaction-list-panel";
import type { TransactionGroup } from "@/components/panels/dashboard-data";

type SaidasPanelProps = {
  groups: TransactionGroup[];
  total: number;
};

export function SaidasPanel({ groups, total }: SaidasPanelProps) {
  return (
    <TransactionListPanel
      emptyLabel="Sem saidas neste board."
      groups={groups}
      title="Saidas"
      tone="saida"
      total={total}
    />
  );
}
