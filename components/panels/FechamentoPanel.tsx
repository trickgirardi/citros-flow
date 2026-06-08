import { formatCurrency } from "@/components/panels/dashboard-data";
import { ImportNubankCsvModal } from "@/components/board/ImportNubankCsvModal";
import {
  type ClosingCategorySummary,
  MonthlyClosingImageModal,
} from "@/components/board/MonthlyClosingImageModal";
import { TransactionModal } from "@/components/modals/TransactionModal";
import { cn } from "@/lib/utils";

type FechamentoPanelProps = {
  balance: number;
  boardName: string;
  boardId: string;
  canMutate: boolean;
  categorySuggestions: string[];
  descriptionSuggestions: string[];
  entradaCategories: ClosingCategorySummary[];
  entradasTotal: number;
  monthLabel: string;
  previousBalance: number;
  saidaCategories: ClosingCategorySummary[];
  saidasTotal: number;
  transactionCount: number;
};

export function FechamentoPanel({
  balance,
  boardName,
  boardId,
  canMutate,
  categorySuggestions,
  descriptionSuggestions,
  entradaCategories,
  entradasTotal,
  monthLabel,
  previousBalance,
  saidaCategories,
  saidasTotal,
  transactionCount,
}: FechamentoPanelProps) {
  const balanceIsPositive = balance >= 0;

  return (
    <aside className="flex shrink-0 flex-col rounded-lg border bg-card md:min-h-0">
      <div className="border-b px-3 py-2">
        <h2 className="text-sm font-semibold">Fechamento</h2>
        <p className="text-xs text-muted-foreground">{transactionCount} transacoes</p>
      </div>

      <div className="grid gap-2 p-3">
        <SummaryRow label="Saldo anterior" value={formatCurrency(previousBalance)} />
        <SummaryRow label="Total entradas" value={formatCurrency(entradasTotal)} />
        <SummaryRow label="Total saidas" value={formatCurrency(saidasTotal)} />

        <div className="mt-1 rounded-md border bg-background p-3">
          <p className="text-xs text-muted-foreground">Saldo final</p>
          <p
            className={cn(
              "mt-1 text-2xl font-semibold tabular-nums",
              balanceIsPositive
                ? "text-emerald-700 dark:text-emerald-300"
                : "text-rose-700 dark:text-rose-300"
            )}
          >
            {formatCurrency(balance)}
          </p>
        </div>
      </div>

      <div className="mt-auto grid gap-2 border-t p-3">
        <MonthlyClosingImageModal
          boardName={boardName}
          entradas={entradaCategories}
          monthLabel={monthLabel}
          previousBalance={previousBalance}
          saidas={saidaCategories}
        />
        {canMutate ? (
          <>
            <TransactionModal
              boardId={boardId}
              categorySuggestions={categorySuggestions}
              descriptionSuggestions={descriptionSuggestions}
            />
            <ImportNubankCsvModal boardId={boardId} />
          </>
        ) : null}
      </div>
    </aside>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-md bg-muted px-3 py-2">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-semibold tabular-nums">{value}</span>
    </div>
  );
}
