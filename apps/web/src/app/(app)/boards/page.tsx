import Link from "next/link"
import { notFound } from "next/navigation"
import { Landmark } from "lucide-react"

import { BoardFormModal } from "@/components/boards/BoardFormModal"
import { describeBoardContents } from "@/components/boards/board-contents"
import { DeleteBoardModal } from "@/components/boards/DeleteBoardModal"
import { PageContent } from "@/components/layout/page-content"
import { PageHeader } from "@/components/layout/page-header"
import { createApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"
import { getCurrentUser } from "@/lib/auth"

const dateFormatter = new Intl.DateTimeFormat("pt-BR", {
  timeZone: "America/Sao_Paulo",
})

export default async function BoardsPage() {
  const user = await getCurrentUser()

  // Só admin global gerencia boards; para os demais a tela nem existe.
  if (!user?.canManageBoards) {
    notFound()
  }

  const api = await createApiClient()
  const boards = readData(await api.GET("/api/admin/boards"))

  return (
    <>
      <PageHeader title="Boards" />
      <PageContent className="overflow-y-auto">
        <div className="mx-auto grid max-w-3xl gap-4">
          <div className="flex items-center justify-between gap-3">
            <p className="text-base text-muted-foreground">
              Cada board tem o seu proprio fluxo de caixa.
            </p>
            <BoardFormModal />
          </div>

          {boards.length === 0 ? (
            <p className="rounded-card border bg-card p-6 text-base text-muted-foreground shadow-card">
              Nenhum board criado ainda. Use &quot;Novo board&quot; para criar o
              primeiro.
            </p>
          ) : (
            <ul className="grid gap-2">
              {boards.map((board) => (
                <li
                  key={board.id}
                  className="flex items-center justify-between gap-3 rounded-card border bg-card p-3 shadow-card"
                >
                  <Link
                    href={`/board/${board.id}/fluxo-de-caixa`}
                    className="flex min-w-0 items-center gap-3"
                  >
                    <span className="flex size-10 shrink-0 items-center justify-center rounded-field border bg-muted text-accent-ink">
                      <Landmark className="size-4" />
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate text-base font-bold">
                        {board.name}
                      </span>
                      <span className="block truncate text-sm text-muted-foreground">
                        {describeBoardContents(board)} · criado em{" "}
                        {dateFormatter.format(new Date(board.createdAt))}
                      </span>
                    </span>
                  </Link>
                  <div className="flex shrink-0 items-center gap-1">
                    <BoardFormModal board={board} />
                    <DeleteBoardModal board={board} />
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </PageContent>
    </>
  )
}
