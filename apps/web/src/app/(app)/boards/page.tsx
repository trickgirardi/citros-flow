import Link from "next/link"
import { notFound } from "next/navigation"
import { ArrowLeft, Landmark } from "lucide-react"

import { BoardFormModal } from "@/components/boards/BoardFormModal"
import { describeBoardContents } from "@/components/boards/board-contents"
import { DeleteBoardModal } from "@/components/boards/DeleteBoardModal"
import { LogoutButton } from "@/components/layout/logout-button"
import { ThemeToggle } from "@/components/theme-provider"
import { Button } from "@/components/ui/button"
import { LogoSymbol } from "@/components/ui/logo"
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
    <section className="flex h-full min-h-0 flex-col overflow-hidden">
      <header className="on-brand flex shrink-0 items-center justify-between gap-3 bg-brand px-3 py-2 text-on-brand sm:px-4">
        <div className="flex min-w-0 items-center gap-3">
          <LogoSymbol size={36} decorative />
          <h1 className="text-lg">Boards</h1>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Button asChild variant="on-brand" className="h-8 px-3">
            <Link href="/board">
              <ArrowLeft />
              Voltar
            </Link>
          </Button>
          <ThemeToggle variant="on-brand-ghost" />
          <LogoutButton variant="on-brand" />
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto p-3 sm:p-4">
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
                    href={`/board/${board.id}`}
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
      </div>
    </section>
  )
}
