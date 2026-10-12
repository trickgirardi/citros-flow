import { BoardHeader } from "@/components/board/BoardHeader";
import { getBoard } from "@/lib/boards";

type BoardLayoutProps = {
  children: React.ReactNode;
  params: Promise<{
    boardId: string;
  }>;
};

export default async function BoardLayout({ children, params }: BoardLayoutProps) {
  const { boardId } = await params;
  const board = await getBoard(boardId); // 404 se o board não existe ou o usuário não o vê

  return (
    <section className="flex h-full min-h-0 flex-col overflow-hidden bg-muted/30">
      <BoardHeader
        boardId={board.id}
        boardName={board.name}
        canMutate={board.canMutate}
      />
      <div className="min-h-0 flex-1 overflow-hidden p-3 sm:p-4">{children}</div>
    </section>
  );
}
