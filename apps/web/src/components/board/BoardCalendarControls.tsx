"use client";

import { useSearchParams } from "next/navigation";

import { MonthNavigator } from "@/components/board/MonthNavigator";
import {
  getCurrentMonthKey,
  getMonthScope,
  getSingleSearchParam,
} from "@/components/board/month-scope";

type BoardCalendarControlsProps = {
  boardId: string;
};

export function BoardCalendarControls({ boardId }: BoardCalendarControlsProps) {
  const searchParams = useSearchParams();
  const monthScope = getMonthScope(
    getSingleSearchParam(searchParams.get("month") ?? undefined)
  );

  return (
    <>
      <div className="hidden sm:block">
        <MonthNavigator
          boardId={boardId}
          currentMonthKey={getCurrentMonthKey()}
          label={monthScope.label}
          nextMonthKey={monthScope.nextMonthKey}
          previousMonthKey={monthScope.previousMonthKey}
        />
      </div>
      <div className="sm:hidden">
        <MonthNavigator
          boardId={boardId}
          currentMonthKey={getCurrentMonthKey()}
          label={monthScope.label}
          mode="menu"
          nextMonthKey={monthScope.nextMonthKey}
          previousMonthKey={monthScope.previousMonthKey}
        />
      </div>
    </>
  );
}
