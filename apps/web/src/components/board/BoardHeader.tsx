import Link from "next/link";
import { Menu } from "lucide-react";

import { BoardCalendarControls } from "@/components/board/BoardCalendarControls";
import { ShareBoardButton } from "@/components/board/ShareBoardButton";
import { LogoutButton } from "@/components/layout/logout-button";
import { ThemeToggle } from "@/components/theme-provider";
import { LogoSymbol } from "@/components/ui/logo";

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
    <header className="on-brand flex shrink-0 items-center justify-between gap-3 bg-brand px-3 py-2 text-on-brand sm:grid sm:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] sm:px-4">
      <div className="flex min-w-0 items-center gap-3">
        <LogoSymbol size={36} decorative />
        <h1 className="min-w-0 truncate text-lg">{boardName}</h1>
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
          <summary className="flex h-8 list-none items-center justify-center rounded-pill border-2 border-white/50 px-3 text-on-brand hover:bg-white/15">
            <Menu />
            <span className="sr-only">Abrir menu</span>
          </summary>
          <div className="absolute right-0 top-10 z-10 grid min-w-52 gap-1 rounded-field border bg-popover p-2 text-popover-foreground shadow-card">
            {boards.length > 1 ? (
              <nav aria-label="Boards" className="grid gap-1 border-b pb-2">
                <p className="px-2 text-xs text-muted-foreground">Boards</p>
                {boards.map((item) => (
                  <Link
                    key={item.id}
                    href={`/board/${item.id}`}
                    aria-current={item.id === boardId ? "page" : undefined}
                    className="truncate rounded-lg px-2 py-1.5 text-sm hover:bg-accent aria-[current=page]:bg-accent aria-[current=page]:font-semibold"
                  >
                    {item.name}
                  </Link>
                ))}
              </nav>
            ) : null}
            {canManageBoards ? (
              <Link
                href="/boards"
                className="rounded-lg px-2 py-1.5 text-sm hover:bg-accent"
              >
                Gerenciar boards
              </Link>
            ) : null}
            <ThemeToggle
              showLabel
              className="h-auto justify-start gap-2 rounded-lg px-2 py-1.5 text-sm font-normal"
            />
            <LogoutButton />
          </div>
        </details>
      </div>
    </header>
  );
}
