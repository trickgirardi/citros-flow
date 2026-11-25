import { redirect } from "next/navigation"

import {
  getMonthScope,
  getSingleSearchParam,
} from "@/components/board/month-scope"
import { PageContent } from "@/components/layout/page-content"
import { PageHeader } from "@/components/layout/page-header"
import { createApiClient } from "@/lib/api-client"
import { readData } from "@/lib/api-result"
import { getCurrentUser } from "@/lib/auth"

type BoardPageProps = {
  searchParams: Promise<{
    month?: string | string[]
  }>
}

export default async function BoardPage({ searchParams }: BoardPageProps) {
  const { month } = await searchParams
  const monthScope = getMonthScope(getSingleSearchParam(month))
  const api = await createApiClient()
  const boards = readData(await api.GET("/api/boards"))
  const firstBoard = boards[0]

  if (firstBoard) {
    redirect(
      `/board/${firstBoard.id}/fluxo-de-caixa?month=${monthScope.monthKey}`
    )
  }

  // Sem nenhum board, o admin global cria o primeiro na tela de gestão.
  if ((await getCurrentUser())?.canManageBoards) {
    redirect("/boards")
  }

  return (
    <>
      <PageHeader title="Gnosis - Fluxo de Caixa" />
      <PageContent className="flex items-center justify-center">
        <div className="w-full max-w-md rounded-panel border bg-card p-6 shadow-card">
          <h1 className="text-2xl">Nenhum board vinculado</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Sua conta ainda nao tem acesso a um board financeiro.
          </p>
        </div>
      </PageContent>
    </>
  )
}
