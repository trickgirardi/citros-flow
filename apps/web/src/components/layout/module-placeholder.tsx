import { Construction } from "lucide-react"

import { BOARD_MODULES } from "@/components/layout/nav-items"
import { PageContent } from "@/components/layout/page-content"
import { PageHeader } from "@/components/layout/page-header"
import { getBoard } from "@/lib/boards"

/** Página "Em construção" dos módulos que ainda não existem (dashboard, tarefas, estoque). */
export async function ModulePlaceholder({
  boardId,
  segment,
}: {
  boardId: string
  segment: string
}) {
  const board = await getBoard(boardId)
  const item = BOARD_MODULES.find((candidate) => candidate.segment === segment)
  const title = item?.label ?? "Em construção"

  return (
    <>
      <PageHeader scope={board.name} title={title} />
      <PageContent className="flex items-center justify-center">
        <div className="w-full max-w-md rounded-panel border bg-card p-8 text-center shadow-card">
          <Construction className="mx-auto mb-4 size-10 text-accent-ink" />
          <h2 className="font-sans text-xl font-bold">Em construção</h2>
          <p className="mt-2 text-base text-muted-foreground">
            {title} ainda será implementado. O módulo aparece aqui quando
            estiver pronto.
          </p>
        </div>
      </PageContent>
    </>
  )
}
