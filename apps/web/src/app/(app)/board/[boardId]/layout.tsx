import { getBoard } from "@/lib/boards"

type BoardLayoutProps = {
  children: React.ReactNode
  params: Promise<{
    boardId: string
  }>
}

export default async function BoardLayout({
  children,
  params,
}: BoardLayoutProps) {
  const { boardId } = await params

  await getBoard(boardId) // 404 se o board não existe ou o usuário não o vê

  return children
}
