import { redirect } from "next/navigation"

import { getSingleSearchParam } from "@/components/board/month-scope"

type BoardIndexPageProps = {
  params: Promise<{ boardId: string }>
  searchParams: Promise<{ month?: string | string[] }>
}

export default async function BoardIndexPage({
  params,
  searchParams,
}: BoardIndexPageProps) {
  const [{ boardId }, { month }] = await Promise.all([params, searchParams])
  const monthParam = getSingleSearchParam(month)
  const query = monthParam ? `?month=${encodeURIComponent(monthParam)}` : ""

  // Com `?month=` é o endereço antigo do fluxo de caixa: segue funcionando, com o mês. Sem mês, vai
  // à tela inicial do board (o dashboard).
  redirect(
    monthParam
      ? `/board/${boardId}/fluxo-de-caixa${query}`
      : `/board/${boardId}/dashboard`
  )
}
