"use client";

import { useRef, useState, useActionState } from "react";
import { useRouter } from "next/navigation";
import { Plus } from "lucide-react";

import {
  createTransaction,
  type TransactionFormState,
} from "@/app/(app)/board/[boardId]/actions";
import { Button } from "@/components/ui/button";
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
};

export function TransactionModal({ boardId }: TransactionModalProps) {
  const [open, setOpen] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const router = useRouter();
  const [state, formAction, isPending] = useActionState(
    async (currentState: TransactionFormState, formData: FormData) => {
      const nextState = await createTransaction(currentState, formData);

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
        <Button type="button" className="h-9 w-full">
          <Plus data-icon="inline-start" />
          Nova Transacao
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nova Transacao</DialogTitle>
          <DialogDescription>
            Registre entrada ou saida para atualizar o fechamento.
          </DialogDescription>
        </DialogHeader>

        <form ref={formRef} action={formAction} className="grid gap-4">
          <input type="hidden" name="boardId" value={boardId} />

          <label className="grid gap-1.5 text-sm font-medium">
            Tipo
            <select
              name="type"
              required
              className="h-9 rounded-md border bg-background px-3 text-sm"
              defaultValue="entrada"
            >
              <option value="entrada">Entrada</option>
              <option value="saida">Saida</option>
            </select>
          </label>

          <label className="grid gap-1.5 text-sm font-medium">
            Valor
            <input
              name="amount"
              type="number"
              inputMode="decimal"
              min="0.01"
              step="0.01"
              required
              className="h-9 rounded-md border bg-background px-3 text-sm"
              placeholder="0,00"
            />
          </label>

          <label className="grid gap-1.5 text-sm font-medium">
            Descricao
            <input
              name="description"
              type="text"
              required
              maxLength={120}
              className="h-9 rounded-md border bg-background px-3 text-sm"
              placeholder="Ex: Doacao mensal"
            />
          </label>

          <label className="grid gap-1.5 text-sm font-medium">
            Categoria
            <input
              name="category"
              type="text"
              required
              maxLength={80}
              className="h-9 rounded-md border bg-background px-3 text-sm"
              placeholder="Ex: Doacoes"
            />
          </label>

          <label className="grid gap-1.5 text-sm font-medium">
            Data
            <input
              name="date"
              type="date"
              required
              className="h-9 rounded-md border bg-background px-3 text-sm"
              defaultValue={new Date().toISOString().slice(0, 10)}
            />
          </label>

          {state.error ? (
            <p className="text-sm text-destructive">{state.error}</p>
          ) : null}

          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              disabled={isPending}
              onClick={() => setOpen(false)}
            >
              Cancelar
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? "Salvando..." : "Salvar"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
