import {
  formatCurrency,
  formatTransactionDate,
  type TransactionGroup,
} from "@/components/panels/dashboard-data";
import { cn } from "@/lib/utils";

type TransactionListPanelProps = {
  emptyLabel: string;
  groups: TransactionGroup[];
  tone: "entrada" | "saida";
  title: string;
  total: number;
};

export function TransactionListPanel({
  emptyLabel,
  groups,
  tone,
  title,
  total,
}: TransactionListPanelProps) {
  const amountClassName =
    tone === "entrada"
      ? "text-emerald-700 dark:text-emerald-300"
      : "text-rose-700 dark:text-rose-300";

  return (
    <section className="flex min-h-0 flex-1 flex-col rounded-lg border bg-card md:flex-none">
      <div className="flex shrink-0 items-center justify-between border-b px-3 py-2">
        <div>
          <h2 className="text-sm font-semibold">{title}</h2>
          <p className="text-xs text-muted-foreground">{formatCurrency(total)}</p>
        </div>
        <span className={cn("text-xs font-medium", amountClassName)}>{groups.length}</span>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-2">
        {groups.length === 0 ? (
          <div className="flex h-full min-h-24 items-center justify-center rounded-md border border-dashed text-center text-xs text-muted-foreground">
            {emptyLabel}
          </div>
        ) : (
          <div className="space-y-4">
            {groups.map((group) => (
              <section key={group.category} className="space-y-2">
                <div className="flex items-center justify-between gap-3 border-b pb-1">
                  <h3 className="truncate text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    {group.category}
                  </h3>
                  <span className="shrink-0 text-xs font-medium">
                    {formatCurrency(group.total)}
                  </span>
                </div>

                <div className="space-y-1.5">
                  {group.transactions.map((transaction) => (
                    <article
                      key={transaction.id}
                      className="grid grid-cols-[2.5rem_1fr_auto] items-center gap-2 rounded-md border bg-background px-2 py-1.5"
                    >
                      <span className="text-xs text-muted-foreground">
                        {formatTransactionDate(transaction.date)}
                      </span>
                      <span className="min-w-0 truncate text-sm">
                        {transaction.description}
                      </span>
                      <span
                        className={cn("text-xs font-semibold tabular-nums", amountClassName)}
                      >
                        {formatCurrency(transaction.amount)}
                      </span>
                    </article>
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
