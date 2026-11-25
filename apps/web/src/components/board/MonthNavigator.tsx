import Link from "next/link";
import type { ReactNode } from "react";
import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";

import { Button } from "@/components/ui/button";

type MonthNavigatorProps = {
  boardId: string;
  currentMonthKey: string;
  label: string;
  nextMonthKey: string;
  previousMonthKey: string;
  mode?: "full" | "menu";
};

export function MonthNavigator({
  boardId,
  currentMonthKey,
  label,
  nextMonthKey,
  previousMonthKey,
  mode = "full",
}: MonthNavigatorProps) {
  if (mode === "menu") {
    return (
      <details className="relative">
        <summary className="flex h-8 list-none items-center justify-center rounded-pill border-2 border-white/50 px-3 text-sm font-medium text-on-brand hover:bg-white/15">
          <CalendarDays data-icon="inline-start" />
          <span className="sr-only">Abrir calendario</span>
        </summary>
        <div className="absolute right-0 top-10 z-10 grid min-w-44 gap-1 rounded-field border bg-popover p-2 text-popover-foreground shadow-card">
          <p className="px-2 pb-1 text-xs font-medium text-muted-foreground first-letter:uppercase">
            {label}
          </p>
          <MonthMenuLink href={getBoardMonthHref(boardId, previousMonthKey)}>
            Mes anterior
          </MonthMenuLink>
          <MonthMenuLink href={getBoardMonthHref(boardId, nextMonthKey)}>
            Proximo mes
          </MonthMenuLink>
          <MonthMenuLink href={getBoardMonthHref(boardId, currentMonthKey)}>
            Hoje
          </MonthMenuLink>
        </div>
      </details>
    );
  }

  return (
    <nav
      aria-label="Navegacao mensal"
      className="flex shrink-0 items-center gap-1 rounded-pill border-2 border-white/50 p-1 text-on-brand"
    >
      <Button asChild type="button" variant="on-brand-ghost" className="h-8 px-2">
        <Link href={getBoardMonthHref(boardId, previousMonthKey)} aria-label="Mes anterior">
          <ChevronLeft data-icon="inline-start" />
        </Link>
      </Button>

      <div className="flex min-w-32 items-center justify-center gap-2 px-2 text-sm font-medium">
        <CalendarDays data-icon="inline-start" />
        <span className="inline-block first-letter:uppercase">{label}</span>
      </div>

      <Button asChild type="button" variant="on-brand-ghost" className="h-8 px-2">
        <Link href={getBoardMonthHref(boardId, nextMonthKey)} aria-label="Proximo mes">
          <ChevronRight data-icon="inline-start" />
        </Link>
      </Button>

      <Button asChild type="button" variant="on-brand" className="h-8 px-3 text-xs">
        <Link href={getBoardMonthHref(boardId, currentMonthKey)}>Hoje</Link>
      </Button>
    </nav>
  );
}

function MonthMenuLink({
  children,
  href,
}: {
  children: ReactNode;
  href: string;
}) {
  return (
    <Link
      href={href}
      className="rounded-lg px-2 py-1.5 text-sm hover:bg-accent"
    >
      {children}
    </Link>
  );
}

function getBoardMonthHref(boardId: string, monthKey: string) {
  return `/board/${boardId}/fluxo-de-caixa?month=${monthKey}`;
}
