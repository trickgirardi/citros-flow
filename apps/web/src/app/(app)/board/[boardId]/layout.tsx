import { notFound } from "next/navigation";

import { BoardHeader } from "@/components/board/BoardHeader";
import { getBoardForCurrentUser } from "@/lib/supabase/queries/boards";

type BoardLayoutProps = {
  children: React.ReactNode;
  params: Promise<{
    boardId: string;
  }>;
};

export default async function BoardLayout({ children, params }: BoardLayoutProps) {
  const { boardId } = await params;
  const board = await getBoardForCurrentUser(boardId);

  if (!board) {
    notFound();
  }

  return (
    <section className="flex h-full min-h-0 flex-col overflow-hidden bg-muted/30">
      <BoardHeader boardId={board.id} boardName={board.name} />
      <div className="min-h-0 flex-1 overflow-hidden p-3 sm:p-4">{children}</div>
    </section>
  );
}
