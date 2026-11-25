import { ModulePlaceholder } from "@/components/layout/module-placeholder"

export default async function Page({
  params,
}: {
  params: Promise<{ boardId: string }>
}) {
  const { boardId } = await params

  return <ModulePlaceholder boardId={boardId} segment="tarefas" />
}
