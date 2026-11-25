import { redirect } from "next/navigation"

import { getSingleSearchParam } from "@/components/board/month-scope"

type BoardIndexPageProps = {
  params: Promise<{ boardId: string }>
  searchParams: Promise<{ month?: string | string[] }>
}

// Endereço antigo do fluxo de caixa (/board/{id}?month=...): segue funcionando, com o mês.
export default async function BoardIndexPage({
  params,
  searchParams,
}: BoardIndexPageProps) {
  const [{ boardId }, { month }] = await Promise.all([params, searchParams])
  const monthParam = getSingleSearchParam(month)
  const query = monthParam ? `?month=${encodeURIComponent(monthParam)}` : ""

  redirect(`/board/${boardId}/fluxo-de-caixa${query}`)
}
