"use client";

import { useActionState, useState } from "react";
import { useRouter } from "next/navigation";
import { Check, X } from "lucide-react";

import {
  type UpdateTransactionDescriptionState,
  updateTransactionDescription,
} from "@/app/(app)/board/[boardId]/actions";
import { Button } from "@/components/ui/button";

const INITIAL_STATE: UpdateTransactionDescriptionState = {
  error: null,
  success: false,
};

type InlineDescriptionEditorProps = {
  boardId: string;
  description: string;
  transactionId: string;
};

export function InlineDescriptionEditor({
  boardId,
  description: initialDescription,
  transactionId,
}: InlineDescriptionEditorProps) {
  const router = useRouter();
  const [description, setDescription] = useState(initialDescription);
  const [isEditing, setIsEditing] = useState(false);
  const [state, formAction, isPending] = useActionState(
    async (currentState: UpdateTransactionDescriptionState, formData: FormData) => {
      const nextState = await updateTransactionDescription(currentState, formData);

      if (nextState.success) {
        setIsEditing(false);
        router.refresh();
      }

      return nextState;
    },
    INITIAL_STATE
  );
  const cleanDescription = description.trim();
  const hasChanged = cleanDescription !== initialDescription.trim();

  return (
    <form action={formAction} className="min-w-0">
      <input type="hidden" name="boardId" value={boardId} />
      <input type="hidden" name="transactionId" value={transactionId} />

      <div className="relative">
        <input
          name="description"
          type="text"
          maxLength={120}
          required
          readOnly={!isEditing}
          value={description}
          className="h-8 w-full truncate rounded-md border border-transparent bg-transparent px-2 pr-16 text-sm outline-none transition-colors focus:border-border focus:bg-background"
          onChange={(event) => setDescription(event.target.value)}
          onFocus={() => setIsEditing(true)}
          onClick={() => setIsEditing(true)}
        />

        {isEditing ? (
          <div className="absolute right-1 top-1 flex items-center gap-1">
            <Button
              type="submit"
              size="icon-xs"
              disabled={isPending || !hasChanged || cleanDescription.length === 0}
              aria-label="Confirmar alteracao da descricao"
            >
              <Check data-icon="inline-start" />
            </Button>
            <Button
              type="button"
              variant="outline"
              size="icon-xs"
              disabled={isPending}
              aria-label="Cancelar alteracao da descricao"
              onClick={() => {
                setDescription(initialDescription);
                setIsEditing(false);
              }}
            >
              <X data-icon="inline-start" />
            </Button>
          </div>
        ) : null}
      </div>

      {isEditing && state.error ? (
        <p className="mt-1 text-xs text-destructive">{state.error}</p>
      ) : null}
    </form>
  );
}
