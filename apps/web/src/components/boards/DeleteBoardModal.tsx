"use client"

import { useActionState, useState } from "react"
import { useRouter } from "next/navigation"
import { Trash2 } from "lucide-react"

import { deleteBoard, type BoardFormState } from "@/app/(app)/boards/actions"
import {
  describeBoardContents,
  type AdminBoard,
} from "@/components/boards/board-contents"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"

const INITIAL_STATE: BoardFormState = { error: null, success: false }

export function DeleteBoardModal({ board }: { board: AdminBoard }) {
  const [open, setOpen] = useState(false)

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          className="text-destructive hover:bg-destructive/10"
          aria-label={`Remover ${board.name}`}
        >
          <Trash2 data-icon="inline-start" />
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Remover board</DialogTitle>
          <DialogDescription>
            Isto apaga de vez o board <strong>{board.name}</strong> e tudo que
            ele guarda: {describeBoardContents(board)}. Nao da para desfazer.
          </DialogDescription>
        </DialogHeader>
        {/* O formulario so existe com o modal aberto: ao fechar, a confirmacao digitada some. */}
        <DeleteBoardForm board={board} onDone={() => setOpen(false)} />
      </DialogContent>
    </Dialog>
  )
}

function DeleteBoardForm({
  board,
  onDone,
}: {
  board: AdminBoard
  onDone: () => void
}) {
  const router = useRouter()
  const [confirmation, setConfirmation] = useState("")
  const [state, formAction, isPending] = useActionState(
    async (current: BoardFormState, formData: FormData) => {
      const next = await deleteBoard(current, formData)

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
      <input type="hidden" name="boardId" value={board.id} />

      <label className="grid gap-1.5 text-base font-semibold">
        Para confirmar, digite o nome do board
        <Input
          type="text"
          autoComplete="off"
          autoFocus
          value={confirmation}
          onChange={(event) => setConfirmation(event.target.value)}
          placeholder={board.name}
        />
      </label>

      {state.error ? (
        <p role="alert" className="text-sm font-semibold text-destructive">
          {state.error}
        </p>
      ) : null}

      <div className="flex justify-end">
        <Button
          type="submit"
          variant="destructive"
          size="lg"
          disabled={isPending || confirmation.trim() !== board.name}
        >
          {isPending ? "Removendo..." : "Remover board"}
        </Button>
      </div>
    </form>
  )
}
