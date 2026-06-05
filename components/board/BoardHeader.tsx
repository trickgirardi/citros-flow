import { Landmark, Menu } from "lucide-react";

import { BoardCalendarControls } from "@/components/board/BoardCalendarControls";
import { ShareBoardButton } from "@/components/board/ShareBoardButton";
import { LogoutButton } from "@/components/layout/logout-button";

type BoardHeaderProps = {
  boardId: string;
  boardName: string;
};

export function BoardHeader({ boardId, boardName }: BoardHeaderProps) {
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
        <span className="hidden sm:inline-flex">
          <ShareBoardButton />
        </span>
        <span className="inline-flex sm:hidden">
          <ShareBoardButton showLabel={false} />
        </span>
        <details className="relative">
          <summary className="flex h-8 cursor-pointer list-none items-center justify-center rounded-md border bg-background px-2">
            <Menu />
            <span className="sr-only">Abrir menu</span>
          </summary>
          <div className="absolute right-0 top-10 z-10 rounded-md border bg-background p-2 shadow-lg">
            <LogoutButton />
          </div>
        </details>
      </div>
    </header>
  );
}
