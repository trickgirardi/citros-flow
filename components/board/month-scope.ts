const APP_TIMEZONE = "America/Sao_Paulo";
const MONTH_KEY_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;

const monthFormatter = new Intl.DateTimeFormat("pt-BR", {
  month: "long",
  timeZone: APP_TIMEZONE,
  year: "numeric",
});

const currentMonthFormatter = new Intl.DateTimeFormat("en-CA", {
  month: "2-digit",
  timeZone: APP_TIMEZONE,
  year: "numeric",
});

export type MonthScope = {
  endDate: string;
  label: string;
  monthKey: string;
  nextMonthKey: string;
  previousMonthKey: string;
  startDate: string;
};

export function getMonthScope(monthKey: string | null | undefined): MonthScope {
  const safeMonthKey = parseMonthKey(monthKey) ?? getCurrentMonthKey();
  const [year, month] = parseMonthParts(safeMonthKey);
  const nextMonthKey = shiftMonthKey(safeMonthKey, 1);

  return {
    endDate: `${nextMonthKey}-01`,
    label: formatMonthLabel(safeMonthKey),
    monthKey: safeMonthKey,
    nextMonthKey,
    previousMonthKey: shiftMonthKey(safeMonthKey, -1),
    startDate: `${year}-${String(month).padStart(2, "0")}-01`,
  };
}

export function getCurrentMonthKey() {
  return currentMonthFormatter.format(new Date());
}

export function parseMonthKey(value: string | null | undefined) {
  if (!value || !MONTH_KEY_PATTERN.test(value)) {
    return null;
  }

  return value;
}

export function getSingleSearchParam(value: string | string[] | undefined) {
  if (Array.isArray(value)) {
    return value[0];
  }

  return value;
}

function shiftMonthKey(monthKey: string, offset: number) {
  const [year, month] = parseMonthParts(monthKey);
  const date = new Date(Date.UTC(year, month - 1 + offset, 1));

  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, "0")}`;
}

function formatMonthLabel(monthKey: string) {
  const [year, month] = parseMonthParts(monthKey);
  const date = new Date(Date.UTC(year, month - 1, 1));

  return monthFormatter.format(date);
}

function parseMonthParts(monthKey: string) {
  const [year, month] = monthKey.split("-").map(Number);

  return [year, month] as const;
}
