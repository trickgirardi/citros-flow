"use client";

import { useId, useRef, useState, useActionState } from "react";
import { useRouter } from "next/navigation";
import { Pencil, Plus } from "lucide-react";

import {
  createTransaction,
  type TransactionFormState,
  updateTransaction,
} from "@/app/(app)/board/[boardId]/actions";
import { getCurrentDateKey } from "@/components/board/month-scope";
import type { Transaction } from "@/components/panels/dashboard-data";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";

const INITIAL_STATE: TransactionFormState = {
  error: null,
  success: false,
};

type TransactionModalProps = {
  boardId: string;
  categorySuggestions?: string[];
  descriptionSuggestions?: string[];
  transaction?: Transaction;
};

export function TransactionModal({
  boardId,
  categorySuggestions = [],
  descriptionSuggestions = [],
  transaction,
}: TransactionModalProps) {
  const categoryListId = useId();
  const descriptionListId = useId();
  const [open, setOpen] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const router = useRouter();
  const isEditing = Boolean(transaction);
  const [state, formAction, isPending] = useActionState(
    async (currentState: TransactionFormState, formData: FormData) => {
      const nextState = isEditing
        ? await updateTransaction(currentState, formData)
        : await createTransaction(currentState, formData);

      if (nextState.success) {
        formRef.current?.reset();
        setOpen(false);
        router.refresh();
      }

      return nextState;
    },
    INITIAL_STATE
  );

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {isEditing ? (
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Editar transacao"
          >
            <Pencil data-icon="inline-start" />
          </Button>
        ) : (
          <Button type="button" className="h-9 w-full">
            <Plus data-icon="inline-start" />
            Nova Transacao
          </Button>
        )}
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {isEditing ? "Editar Transacao" : "Nova Transacao"}
          </DialogTitle>
          <DialogDescription>
            {isEditing
              ? "Atualize os dados desta transacao."
              : "Registre entrada ou saida para atualizar o fechamento."}
          </DialogDescription>
        </DialogHeader>

        <form ref={formRef} action={formAction} className="grid gap-4">
          <input type="hidden" name="boardId" value={boardId} />
          {transaction ? (
            <input type="hidden" name="transactionId" value={transaction.id} />
          ) : null}

          <label className="grid gap-1.5 text-base font-semibold">
            Tipo
            <select
              name="type"
              required
              className="min-h-11 rounded-field border border-input bg-card px-4 py-2 text-base font-normal"
              defaultValue={transaction?.type ?? "entrada"}
            >
              <option value="entrada">Entrada</option>
              <option value="saida">Saida</option>
            </select>
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Valor
            <Input
              name="amount"
              type="number"
              inputMode="decimal"
              min="0.01"
              step="0.01"
              required
              defaultValue={transaction?.amount}
              placeholder="0,00"
            />
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Descricao
            <Input
              name="description"
              type="text"
              list={descriptionListId}
              required
              maxLength={120}
              defaultValue={transaction?.description}
              placeholder="Ex: Doacao mensal"
            />
            <datalist id={descriptionListId}>
              {descriptionSuggestions.map((description) => (
                <option key={description} value={description} />
              ))}
            </datalist>
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Categoria
            <Input
              name="category"
              type="text"
              list={categoryListId}
              required
              maxLength={80}
              defaultValue={transaction?.category}
              placeholder="Ex: Doacoes"
            />
            <datalist id={categoryListId}>
              {categorySuggestions.map((category) => (
                <option key={category} value={category} />
              ))}
            </datalist>
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Data
            <Input
              name="date"
              type="date"
              required
              defaultValue={transaction?.date ?? getCurrentDateKey()}
            />
          </label>

          {state.error ? (
            <p className="text-sm text-destructive">{state.error}</p>
          ) : null}

          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="lg"
              disabled={isPending}
              onClick={() => setOpen(false)}
            >
              Cancelar
            </Button>
            <Button type="submit" size="lg" disabled={isPending}>
              {isPending ? "Salvando..." : isEditing ? "Atualizar" : "Salvar"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
