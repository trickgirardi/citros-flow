"use client";

import { useActionState, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { PiggyBank, Pencil, Trash2 } from "lucide-react";

import {
  createFinancialReserve,
  deleteFinancialReserve,
  type DeleteFinancialReserveState,
  type FinancialReserveFormState,
  updateFinancialReserve,
} from "@/app/(app)/board/[boardId]/actions";
import { formatCurrency } from "@/components/panels/dashboard-data";
import type { FinancialReserve } from "@/components/panels/dashboard-data";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";

const INITIAL_FORM_STATE: FinancialReserveFormState = {
  error: null,
  success: false,
};

const INITIAL_DELETE_STATE: DeleteFinancialReserveState = {
  error: null,
  success: false,
};

type FinancialReservesModalProps = {
  boardId: string;
  reserves: FinancialReserve[];
};

export function FinancialReservesModal({
  boardId,
  reserves,
}: FinancialReservesModalProps) {
  const [open, setOpen] = useState(false);
  const [editingReserve, setEditingReserve] = useState<FinancialReserve | null>(
    null
  );
  const formRef = useRef<HTMLFormElement>(null);
  const router = useRouter();
  const isEditing = Boolean(editingReserve);
  const [state, formAction, isPending] = useActionState(
    async (currentState: FinancialReserveFormState, formData: FormData) => {
      const nextState = isEditing
        ? await updateFinancialReserve(currentState, formData)
        : await createFinancialReserve(currentState, formData);

      if (nextState.success) {
        formRef.current?.reset();
        setEditingReserve(null);
        router.refresh();
      }

      return nextState;
    },
    INITIAL_FORM_STATE
  );

  function handleOpenChange(nextOpen: boolean) {
    setOpen(nextOpen);

    if (!nextOpen) {
      setEditingReserve(null);
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button type="button" variant="outline" className="h-9 w-full">
          <PiggyBank data-icon="inline-start" />
          Reservas financeiras
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reservas financeiras</DialogTitle>
          <DialogDescription>
            Registre valores guardados (dinheiro, caixinha, investimentos). O
            total e apenas informativo e nao entra no calculo do saldo final.
          </DialogDescription>
        </DialogHeader>

        <div className="grid max-h-64 gap-2 overflow-y-auto">
          {reserves.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Nenhuma reserva cadastrada.
            </p>
          ) : (
            reserves.map((reserve) => (
              <FinancialReserveRow
                key={reserve.id}
                boardId={boardId}
                onEdit={() => setEditingReserve(reserve)}
                reserve={reserve}
              />
            ))
          )}
        </div>

        <form
          ref={formRef}
          action={formAction}
          className="grid gap-4 border-t pt-4"
        >
          <input type="hidden" name="boardId" value={boardId} />
          {editingReserve ? (
            <input type="hidden" name="reserveId" value={editingReserve.id} />
          ) : null}

          <label className="grid gap-1.5 text-sm font-medium">
            Nome
            <input
              key={editingReserve?.id ?? "new-name"}
              name="name"
              type="text"
              required
              maxLength={80}
              className="h-9 rounded-md border bg-background px-3 text-sm"
              defaultValue={editingReserve?.name}
              placeholder="Ex: Caixinha, Poupanca"
            />
          </label>

          <label className="grid gap-1.5 text-sm font-medium">
            Valor
            <input
              key={editingReserve ? `${editingReserve.id}-amount` : "new-amount"}
              name="amount"
              type="number"
              inputMode="decimal"
              min="0.01"
              step="0.01"
              required
              className="h-9 rounded-md border bg-background px-3 text-sm"
              defaultValue={editingReserve?.amount}
              placeholder="0,00"
            />
          </label>

          {state.error ? (
            <p className="text-sm text-destructive">{state.error}</p>
          ) : null}

          <div className="flex justify-end gap-2">
            {editingReserve ? (
              <Button
                type="button"
                variant="outline"
                disabled={isPending}
                onClick={() => {
                  setEditingReserve(null);
                  formRef.current?.reset();
                }}
              >
                Cancelar edicao
              </Button>
            ) : null}
            <Button type="submit" disabled={isPending}>
              {isPending
                ? "Salvando..."
                : isEditing
                  ? "Atualizar reserva"
                  : "Adicionar reserva"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function FinancialReserveRow({
  boardId,
  onEdit,
  reserve,
}: {
  boardId: string;
  onEdit: () => void;
  reserve: FinancialReserve;
}) {
  const router = useRouter();
  const [state, formAction, isPending] = useActionState(
    async (
      currentState: DeleteFinancialReserveState,
      formData: FormData
    ) => {
      const nextState = await deleteFinancialReserve(currentState, formData);

      if (nextState.success) {
        router.refresh();
      }

      return nextState;
    },
    INITIAL_DELETE_STATE
  );

  return (
    <div className="grid gap-1">
      <div className="flex items-center justify-between gap-2 rounded-md border bg-muted px-3 py-2">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium">{reserve.name}</p>
          <p className="text-xs text-muted-foreground tabular-nums">
            {formatCurrency(reserve.amount)}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-1">
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Editar reserva"
            onClick={onEdit}
          >
            <Pencil data-icon="inline-start" />
          </Button>
          <form
            action={formAction}
            onSubmit={(event) => {
              if (!window.confirm("Remover esta reserva?")) {
                event.preventDefault();
              }
            }}
          >
            <input type="hidden" name="boardId" value={boardId} />
            <input type="hidden" name="reserveId" value={reserve.id} />
            <Button
              type="submit"
              variant="destructive"
              size="icon-sm"
              disabled={isPending}
              aria-label="Remover reserva"
            >
              <Trash2 data-icon="inline-start" />
            </Button>
          </form>
        </div>
      </div>
      {state.error ? (
        <p className="text-xs text-destructive">{state.error}</p>
      ) : null}
    </div>
  );
}
