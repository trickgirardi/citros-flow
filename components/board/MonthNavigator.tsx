import Link from "next/link";
import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";

import { Button } from "@/components/ui/button";

type MonthNavigatorProps = {
  boardId: string;
  currentMonthKey: string;
  label: string;
  nextMonthKey: string;
  previousMonthKey: string;
};

export function MonthNavigator({
  boardId,
  currentMonthKey,
  label,
  nextMonthKey,
  previousMonthKey,
}: MonthNavigatorProps) {
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

function getBoardMonthHref(boardId: string, monthKey: string) {
  return `/board/${boardId}?month=${monthKey}`;
}
