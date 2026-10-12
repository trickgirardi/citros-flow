"use client";

import { useActionState, useId, useMemo, useState } from "react";
import { useRouter } from "next/navigation";

import {
  type BulkUpdateTransactionCategoryState,
  updateTransactionsCategory,
} from "@/app/(app)/board/[boardId]/actions";
import {
  formatCurrency,
  formatTransactionDate,
  type TransactionGroup,
} from "@/components/panels/dashboard-data";
import { InlineDescriptionEditor } from "@/components/board/InlineDescriptionEditor";
import { TransactionActions } from "@/components/board/TransactionActions";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const INITIAL_BULK_CATEGORY_STATE: BulkUpdateTransactionCategoryState = {
  error: null,
  success: false,
  updatedCount: 0,
};

type TransactionListPanelProps = {
  boardId: string;
  canMutate: boolean;
  categorySuggestions: string[];
  descriptionSuggestions: string[];
  emptyLabel: string;
  groups: TransactionGroup[];
  tone: "entrada" | "saida";
  title: string;
  total: number;
};

export function TransactionListPanel({
  boardId,
  canMutate,
  categorySuggestions,
  descriptionSuggestions,
  emptyLabel,
  groups,
  tone,
  title,
  total,
}: TransactionListPanelProps) {
  const categoryListId = useId();
  const router = useRouter();
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [state, formAction, isPending] = useActionState(
    async (currentState: BulkUpdateTransactionCategoryState, formData: FormData) => {
      const nextState = await updateTransactionsCategory(currentState, formData);

      if (nextState.success) {
        setSelectedIds([]);
        router.refresh();
      }

      return nextState;
    },
    INITIAL_BULK_CATEGORY_STATE
  );
  const transactionIds = useMemo(
    () => groups.flatMap((group) => group.transactions.map((transaction) => transaction.id)),
    [groups]
  );
  const selectedIdSet = useMemo(() => new Set(selectedIds), [selectedIds]);
  const allVisibleSelected =
    transactionIds.length > 0 && transactionIds.every((id) => selectedIdSet.has(id));
  const amountClassName =
    tone === "entrada"
      ? "text-emerald-700 dark:text-emerald-300"
      : "text-rose-700 dark:text-rose-300";

  function toggleTransaction(transactionId: string) {
    setSelectedIds((currentIds) =>
      currentIds.includes(transactionId)
        ? currentIds.filter((id) => id !== transactionId)
        : [...currentIds, transactionId]
    );
  }

  function toggleAllVisible() {
    setSelectedIds((currentIds) => {
      if (allVisibleSelected) {
        return currentIds.filter((id) => !transactionIds.includes(id));
      }

      return Array.from(new Set([...currentIds, ...transactionIds]));
    });
  }

  function shouldIgnoreCardSelection(target: EventTarget | null) {
    return target instanceof HTMLElement
      ? Boolean(
          target.closest(
            "a,button,input,label,select,textarea,form,[data-skip-card-selection]"
          )
        )
      : true;
  }

  return (
    <section className="flex min-h-0 flex-1 flex-col rounded-lg border bg-card md:flex-none">
      <div className="flex shrink-0 items-center justify-between border-b px-3 py-2">
        <div>
          <h2 className="text-sm font-semibold">{title}</h2>
          <p className="text-xs text-muted-foreground">{formatCurrency(total)}</p>
        </div>
        <div className="flex items-center gap-3">
          {canMutate && transactionIds.length > 0 ? (
            <label className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <input
                type="checkbox"
                checked={allVisibleSelected}
                aria-label="Selecionar transacoes visiveis"
                onChange={toggleAllVisible}
              />
              <span className="hidden sm:inline">Selecionar</span>
            </label>
          ) : null}
          <span className={cn("text-xs font-medium", amountClassName)}>{groups.length}</span>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-2">
        {groups.length === 0 ? (
          <div className="flex h-full min-h-24 items-center justify-center rounded-md border border-dashed text-center text-xs text-muted-foreground">
            {emptyLabel}
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            {canMutate && selectedIds.length > 0 ? (
              <form
                action={formAction}
                className="sticky top-0 z-10 grid gap-2 rounded-md border bg-background p-2 shadow-sm"
              >
                <input type="hidden" name="boardId" value={boardId} />
                {selectedIds.map((transactionId) => (
                  <input
                    key={transactionId}
                    type="hidden"
                    name="transactionIds"
                    value={transactionId}
                  />
                ))}

                <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                  <label className="grid min-w-0 flex-1 gap-1 text-xs font-medium">
                    {selectedIds.length} selecionada(s)
                    <input
                      name="category"
                      type="text"
                      list={categoryListId}
                      required
                      maxLength={80}
                      className="h-8 rounded-md border bg-background px-2 text-sm"
                      placeholder="Nova categoria"
                    />
                    <datalist id={categoryListId}>
                      {categorySuggestions.map((category) => (
                        <option key={category} value={category} />
                      ))}
                    </datalist>
                  </label>

                  <div className="flex gap-2">
                    <Button type="submit" disabled={isPending}>
                      {isPending ? "Aplicando..." : "Aplicar"}
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      disabled={isPending}
                      onClick={() => setSelectedIds([])}
                    >
                      Limpar
                    </Button>
                  </div>
                </div>

                {state.error ? (
                  <p className="text-xs text-destructive">{state.error}</p>
                ) : null}
              </form>
            ) : null}

            {groups.map((group) => (
              <section key={group.category} className="flex flex-col gap-2">
                <div className="flex items-center justify-between gap-3 border-b pb-1">
                  <h3 className="truncate text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    {group.category}
                  </h3>
                  <span className="shrink-0 text-xs font-medium">
                    {formatCurrency(group.total)}
                  </span>
                </div>

                <div className="flex flex-col gap-1.5">
                  {group.transactions.map((transaction) => (
                    <article
                      key={transaction.id}
                      className={cn(
                        "grid items-center gap-2 rounded-md border bg-background px-2 py-1.5",
                        canMutate
                          ? "cursor-pointer grid-cols-[auto_2.5rem_minmax(0,1fr)_auto]"
                          : "grid-cols-[2.5rem_minmax(0,1fr)_auto]"
                      )}
                      onClick={(event) => {
                        if (!canMutate || shouldIgnoreCardSelection(event.target)) {
                          return;
                        }

                        toggleTransaction(transaction.id);
                      }}
                    >
                      {canMutate ? (
                        <input
                          type="checkbox"
                          checked={selectedIdSet.has(transaction.id)}
                          aria-label={`Selecionar ${transaction.description}`}
                          onChange={() => toggleTransaction(transaction.id)}
                        />
                      ) : null}
                      <span className="text-xs text-muted-foreground">
                        {formatTransactionDate(transaction.date)}
                      </span>
                      {canMutate ? (
                        <InlineDescriptionEditor
                          key={transaction.description}
                          boardId={boardId}
                          description={transaction.description}
                          transactionId={transaction.id}
                        />
                      ) : (
                        <span className="min-w-0 truncate text-sm">
                          {transaction.description}
                        </span>
                      )}
                      <span
                        className={cn("text-xs font-semibold tabular-nums", amountClassName)}
                      >
                        {formatCurrency(transaction.amount)}
                      </span>
                      {canMutate ? (
                        <div className="col-span-4 flex justify-end">
                          <TransactionActions
                            boardId={boardId}
                            categorySuggestions={categorySuggestions}
                            descriptionSuggestions={descriptionSuggestions}
                            transaction={transaction}
                          />
                        </div>
                      ) : null}
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
