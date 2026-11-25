import Link from "next/link";
import { Landmark, Menu } from "lucide-react";

import { BoardCalendarControls } from "@/components/board/BoardCalendarControls";
import { ShareBoardButton } from "@/components/board/ShareBoardButton";
import { LogoutButton } from "@/components/layout/logout-button";

type BoardHeaderProps = {
  boardId: string;
  boardName: string;
  boards: { id: string; name: string }[];
  canManageBoards: boolean;
  canMutate: boolean;
};

export function BoardHeader({
  boardId,
  boardName,
  boards,
  canManageBoards,
  canMutate,
}: BoardHeaderProps) {
  return (
    <header className="flex shrink-0 items-center justify-between gap-3 border-b bg-background px-3 py-2 sm:grid sm:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] sm:px-4">
      <div className="flex min-w-0 items-center gap-2">
        <div className="flex size-8 shrink-0 items-center justify-center rounded-md border bg-muted">
          <Landmark />
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{boardName}</p>
        </div>
      </div>

      <div className="hidden justify-center sm:flex">
        <BoardCalendarControls boardId={boardId} />
      </div>

      <div className="flex shrink-0 items-center justify-end gap-2">
        <div className="sm:hidden">
          <BoardCalendarControls boardId={boardId} />
        </div>
        {/* Só quem pode alterar gera link público (viewer receberia 403). */}
        {canMutate ? (
          <>
            <span className="hidden sm:inline-flex">
              <ShareBoardButton boardId={boardId} />
            </span>
            <span className="inline-flex sm:hidden">
              <ShareBoardButton boardId={boardId} showLabel={false} />
            </span>
          </>
        ) : null}
        {/* key: ao trocar de board o layout continua montado, e o menu precisa voltar a fechar. */}
        <details key={boardId} className="relative">
          <summary className="flex h-8 cursor-pointer list-none items-center justify-center rounded-md border bg-background px-2">
            <Menu />
            <span className="sr-only">Abrir menu</span>
          </summary>
          <div className="absolute right-0 top-10 z-10 grid min-w-52 gap-1 rounded-md border bg-background p-2 shadow-lg">
            {boards.length > 1 ? (
              <nav aria-label="Boards" className="grid gap-1 border-b pb-2">
                <p className="px-2 text-xs text-muted-foreground">Boards</p>
                {boards.map((item) => (
                  <Link
                    key={item.id}
                    href={`/board/${item.id}`}
                    aria-current={item.id === boardId ? "page" : undefined}
                    className="truncate rounded-md px-2 py-1.5 text-sm hover:bg-muted aria-[current=page]:bg-muted aria-[current=page]:font-semibold"
                  >
                    {item.name}
                  </Link>
                ))}
              </nav>
            ) : null}
            {canManageBoards ? (
              <Link
                href="/boards"
                className="rounded-md px-2 py-1.5 text-sm hover:bg-muted"
              >
                Gerenciar boards
              </Link>
            ) : null}
            <LogoutButton />
          </div>
        </details>
      </div>
    </header>
  );
}
