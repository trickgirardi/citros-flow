import { redirect } from "next/navigation"

import { listBoardsForCurrentUser } from "@/lib/supabase/queries/boards"

export default async function BoardPage() {
  const boards = await listBoardsForCurrentUser()
  const firstBoard = boards[0]

  if (firstBoard) {
    redirect(`/board/${firstBoard.id}`)
  }

  return (
    <section className="flex h-full items-center justify-center p-4">
      <div className="w-full max-w-md rounded-lg border bg-card p-6 shadow-sm">
        <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
          Gnosis - Fluxo de Caixa
        </p>
        <h1 className="mt-2 text-xl font-semibold">Nenhum board vinculado</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Sua conta ainda nao tem acesso a um board financeiro.
        </p>
      </div>
    </section>
  )
}
