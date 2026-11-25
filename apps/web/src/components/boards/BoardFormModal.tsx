"use client"

import { useActionState, useState } from "react"
import { useRouter } from "next/navigation"
import { Pencil, Plus } from "lucide-react"

import {
  createBoard,
  updateBoard,
  type BoardFormState,
} from "@/app/(app)/boards/actions"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"

const INITIAL_STATE: BoardFormState = { error: null, success: false }

type BoardFormModalProps = {
  /** Sem board = criar; com board = renomear. */
  board?: { id: string; name: string }
}

export function BoardFormModal({ board }: BoardFormModalProps) {
  const [open, setOpen] = useState(false)

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {board ? (
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={`Renomear ${board.name}`}
          >
            <Pencil data-icon="inline-start" />
          </Button>
        ) : (
          <Button type="button" className="h-9">
            <Plus data-icon="inline-start" />
            Novo board
          </Button>
        )}
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{board ? "Renomear board" : "Novo board"}</DialogTitle>
          <DialogDescription>
            {board
              ? "O nome aparece no cabecalho e na troca de board."
              : "Cada board tem o seu proprio fluxo de caixa."}
          </DialogDescription>
        </DialogHeader>
        {/* O formulario so existe com o modal aberto: ao fechar, o erro e o estado somem. */}
        <BoardForm board={board} onDone={() => setOpen(false)} />
      </DialogContent>
    </Dialog>
  )
}

function BoardForm({
  board,
  onDone,
}: BoardFormModalProps & { onDone: () => void }) {
  const router = useRouter()
  const [state, formAction, isPending] = useActionState(
    async (current: BoardFormState, formData: FormData) => {
      const next = board
        ? await updateBoard(current, formData)
        : await createBoard(current, formData)

      if (next.success) {
        onDone()
        router.refresh()
      }

      return next
    },
    INITIAL_STATE
  )

  return (
    <form action={formAction} className="grid gap-4">
      {board ? <input type="hidden" name="boardId" value={board.id} /> : null}

      <label className="grid gap-1.5 text-sm font-medium">
        Nome
        <input
          name="name"
          type="text"
          required
          maxLength={80}
          autoFocus
          defaultValue={board?.name}
          className="h-9 rounded-md border bg-background px-3 text-sm font-normal"
          placeholder="Ex: Departamento de Eventos"
        />
      </label>

      {state.error ? (
        <p role="alert" className="text-sm text-destructive">
          {state.error}
        </p>
      ) : null}

      <div className="flex justify-end">
        <Button type="submit" disabled={isPending}>
          {isPending ? "Salvando..." : board ? "Salvar" : "Criar board"}
        </Button>
      </div>
    </form>
  )
}
