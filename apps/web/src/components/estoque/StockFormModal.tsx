"use client"

import { useActionState, useState, useTransition } from "react"

import {
  createStockItem,
  deleteStockItem,
  updateStockItem,
  type StockFormState,
} from "@/app/(app)/board/[boardId]/estoque/actions"
import {
  priceToInput,
  STOCK_LIMITS,
  STOCK_STATUSES,
  type StockItem,
  type StockPerson,
} from "@/components/estoque/stock-data"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"

const INITIAL_STATE: StockFormState = { error: null, success: false }

/** Criar, ou abrir um item existente (editar; leitura para quem não gerencia). */
export type StockFormTarget =
  | { kind: "create" }
  | { kind: "edit"; item: StockItem }

type StockFormModalProps = {
  boardId: string
  onClose: () => void
  /** Pessoas que podem ser responsáveis (só vem para quem gerencia). */
  people: readonly StockPerson[]
  /** Sem permissão de escrita: tudo desabilitado, só "Fechar". */
  readOnly: boolean
  target: StockFormTarget | null
}

const SELECT_CLASS =
  "min-h-11 rounded-field border border-input bg-card px-4 py-2 text-base font-normal disabled:opacity-60"

export function StockFormModal({
  onClose,
  target,
  ...props
}: StockFormModalProps) {
  return (
    <Dialog open={target !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        {/* O formulário só existe com o modal aberto: ao fechar, o que foi digitado some. */}
        {target ? (
          <StockForm onClose={onClose} target={target} {...props} />
        ) : null}
      </DialogContent>
    </Dialog>
  )
}

function StockForm({
  boardId,
  onClose,
  people,
  readOnly,
  target,
}: Omit<StockFormModalProps, "target"> & { target: StockFormTarget }) {
  const item = target.kind === "edit" ? target.item : null
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [isDeleting, startDelete] = useTransition()
  const [state, formAction, isPending] = useActionState(
    async (current: StockFormState, formData: FormData) => {
      const next = await (item ? updateStockItem : createStockItem)(
        current,
        formData
      )

      if (next.success) {
        onClose()
      }

      return next
    },
    INITIAL_STATE
  )

  // Quem já é o responsável aparece mesmo que tenha saído da lista de candidatos.
  const options =
    item?.assignee && !people.some((person) => person.id === item.assignee?.id)
      ? [...people, item.assignee]
      : people
  const busy = isPending || isDeleting

  function handleDelete() {
    if (!item || !window.confirm("Remover este item?")) {
      return
    }

    setDeleteError(null)
    startDelete(async () => {
      const result = await deleteStockItem(boardId, item.id)

      if (result.error) {
        setDeleteError(result.error)
      } else {
        onClose()
      }
    })
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>
          {readOnly ? "Item" : item ? "Editar item" : "Novo item"}
        </DialogTitle>
        <DialogDescription>
          {readOnly
            ? "Você pode ver os detalhes, mas não alterar o item."
            : "Item e status são obrigatórios."}
        </DialogDescription>
      </DialogHeader>

      <form action={formAction} className="grid gap-4">
        <input type="hidden" name="boardId" value={boardId} />
        {item ? <input type="hidden" name="itemId" value={item.id} /> : null}

        <fieldset disabled={readOnly} className="grid min-w-0 gap-4">
          <label className="grid gap-1.5 text-base font-semibold">
            Item
            <Input
              name="name"
              type="text"
              required
              maxLength={STOCK_LIMITS.name}
              autoFocus={!readOnly}
              defaultValue={item?.name}
              className="font-normal"
            />
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Status
            <select
              name="status"
              required
              defaultValue={item?.status ?? "comprar"}
              className={SELECT_CLASS}
            >
              {STOCK_STATUSES.map((status) => (
                <option key={status.status} value={status.status}>
                  {status.label}
                </option>
              ))}
            </select>
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Responsável
            <select
              name="assigneeId"
              defaultValue={item?.assignee?.id ?? ""}
              className={SELECT_CLASS}
            >
              <option value="">Sem responsável</option>
              {options.map((person) => (
                <option key={person.id} value={person.id}>
                  {person.name}
                </option>
              ))}
            </select>
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Preço
            <Input
              name="price"
              type="text"
              inputMode="decimal"
              defaultValue={priceToInput(item?.price ?? null)}
              placeholder="0,00"
              className="font-normal"
            />
          </label>

          <label className="grid gap-1.5 text-base font-semibold">
            Detalhes
            <Textarea
              name="details"
              maxLength={STOCK_LIMITS.details}
              defaultValue={item?.details}
              className="font-normal"
            />
          </label>
        </fieldset>

        {state.error || deleteError ? (
          <p role="alert" className="text-sm font-semibold text-destructive">
            {state.error ?? deleteError}
          </p>
        ) : null}

        <div className="flex flex-wrap justify-end gap-2">
          {item && !readOnly ? (
            <Button
              type="button"
              variant="ghost"
              size="lg"
              disabled={busy}
              onClick={handleDelete}
              className="mr-auto text-destructive hover:bg-destructive/10"
            >
              {isDeleting ? "Removendo..." : "Remover"}
            </Button>
          ) : null}
          <Button type="button" variant="outline" size="lg" onClick={onClose}>
            {readOnly ? "Fechar" : "Cancelar"}
          </Button>
          {readOnly ? null : (
            <Button type="submit" size="lg" disabled={busy}>
              {isPending ? "Salvando..." : "Salvar"}
            </Button>
          )}
        </div>
      </form>
    </>
  )
}
