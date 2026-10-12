"use client";

import { useActionState, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Upload } from "lucide-react";

import {
  importNubankCsv,
  type ImportNubankCsvState,
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

const INITIAL_STATE: ImportNubankCsvState = {
  error: null,
  importedCount: 0,
  success: false,
};

type ImportNubankCsvModalProps = {
  boardId: string;
};

export function ImportNubankCsvModal({ boardId }: ImportNubankCsvModalProps) {
  const [open, setOpen] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const router = useRouter();
  const [state, formAction, isPending] = useActionState(
    async (currentState: ImportNubankCsvState, formData: FormData) => {
      const nextState = await importNubankCsv(currentState, formData);

      if (nextState.success) {
        formRef.current?.reset();
        router.refresh();
      }

      return nextState;
    },
    INITIAL_STATE
  );

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button type="button" variant="outline" className="h-9 w-full">
          <Upload data-icon="inline-start" />
          Importar CSV Nubank
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Importar CSV Nubank</DialogTitle>
          <DialogDescription>
            Envie um extrato Nubank com colunas Data, Valor, Identificador e Descrição.
          </DialogDescription>
        </DialogHeader>

        <form ref={formRef} action={formAction} encType="multipart/form-data" className="grid gap-4">
          <input type="hidden" name="boardId" value={boardId} />

          <label className="grid gap-1.5 text-sm font-medium">
            Arquivo CSV
            <input
              name="csvFile"
              type="file"
              accept=".csv,text/csv"
              required
              className="h-9 rounded-md border bg-background px-3 py-1.5 text-sm"
            />
          </label>

          <div className="rounded-md border bg-muted p-3 text-xs text-muted-foreground">
            Valores positivos viram entradas. Valores negativos viram saídas.
            Categoria padrão: Dados importados.
          </div>

          {state.error ? (
            <p className="text-sm text-destructive">{state.error}</p>
          ) : null}

          {state.success ? (
            <p className="text-sm text-muted-foreground">
              {state.importedCount} transacoes importadas.
            </p>
          ) : null}

          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              disabled={isPending}
              onClick={() => setOpen(false)}
            >
              Fechar
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? "Importando..." : "Importar"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
