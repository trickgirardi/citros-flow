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
        <summary className="flex h-8 cursor-pointer list-none items-center justify-center rounded-md border bg-background px-2 text-sm font-medium">
          <CalendarDays data-icon="inline-start" />
          <span className="sr-only">Abrir calendario</span>
        </summary>
        <div className="absolute right-0 top-10 z-10 grid min-w-44 gap-1 rounded-md border bg-background p-2 shadow-lg">
          <p className="px-2 pb-1 text-xs font-medium capitalize text-muted-foreground">
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
      className="flex shrink-0 items-center gap-1 rounded-md border bg-background p-1"
    >
      <Button asChild type="button" variant="ghost" className="h-8 px-2">
        <Link href={getBoardMonthHref(boardId, previousMonthKey)} aria-label="Mes anterior">
          <ChevronLeft data-icon="inline-start" />
        </Link>
      </Button>

      <div className="flex min-w-32 items-center justify-center gap-2 px-2 text-sm font-medium">
        <CalendarDays data-icon="inline-start" />
        <span className="capitalize">{label}</span>
      </div>

      <Button asChild type="button" variant="ghost" className="h-8 px-2">
        <Link href={getBoardMonthHref(boardId, nextMonthKey)} aria-label="Proximo mes">
          <ChevronRight data-icon="inline-start" />
        </Link>
      </Button>

      <Button asChild type="button" variant="outline" className="h-8 px-2 text-xs">
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
      className="rounded-sm px-2 py-1.5 text-sm hover:bg-muted"
    >
      {children}
    </Link>
  );
}

function getBoardMonthHref(boardId: string, monthKey: string) {
  return `/board/${boardId}?month=${monthKey}`;
}
