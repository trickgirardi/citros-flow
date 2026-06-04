"use client";

import { useActionState } from "react";
import { Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";

import {
  deleteTransaction,
  type DeleteTransactionState,
} from "@/app/(app)/board/[boardId]/actions";
import { TransactionModal } from "@/components/modals/TransactionModal";
import type { Transaction } from "@/components/panels/dashboard-data";
import { Button } from "@/components/ui/button";

const INITIAL_STATE: DeleteTransactionState = {
  error: null,
  success: false,
};

type TransactionActionsProps = {
  boardId: string;
  categorySuggestions: string[];
  descriptionSuggestions: string[];
  transaction: Transaction;
};

export function TransactionActions({
  boardId,
  categorySuggestions,
  descriptionSuggestions,
  transaction,
}: TransactionActionsProps) {
  const router = useRouter();
  const [state, formAction, isPending] = useActionState(
    async (currentState: DeleteTransactionState, formData: FormData) => {
      const nextState = await deleteTransaction(currentState, formData);

      if (nextState.success) {
        router.refresh();
      }

      return nextState;
    },
    INITIAL_STATE
  );

  return (
    <div className="flex items-center justify-end gap-1">
      <TransactionModal
        boardId={boardId}
        categorySuggestions={categorySuggestions}
        descriptionSuggestions={descriptionSuggestions}
        transaction={transaction}
      />
      <form
        action={formAction}
        onSubmit={(event) => {
          if (!window.confirm("Remover esta transacao?")) {
            event.preventDefault();
          }
        }}
      >
        <input type="hidden" name="boardId" value={boardId} />
        <input type="hidden" name="transactionId" value={transaction.id} />
        <Button
          type="submit"
          variant="destructive"
          size="icon-sm"
          disabled={isPending}
          aria-label="Remover transacao"
        >
          <Trash2 data-icon="inline-start" />
        </Button>
      </form>
      {state.error ? (
        <span className="text-xs text-destructive">{state.error}</span>
      ) : null}
    </div>
  );
}
