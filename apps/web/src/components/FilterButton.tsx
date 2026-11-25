import { cn } from "@/lib/utils"

/** Filtro em pílula com contagem (`aria-pressed`): usado nas barras do estoque e das tarefas. */
export function FilterButton({
  active,
  count,
  label,
  onClick,
}: {
  active: boolean
  count: number
  label: string
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        "inline-flex min-h-8 shrink-0 items-center gap-1.5 rounded-pill border px-3 text-sm font-semibold pointer-coarse:min-h-11",
        active
          ? "border-brand bg-brand text-on-brand"
          : "bg-card hover:bg-accent"
      )}
    >
      {label}
      <span
        className={cn(
          "text-xs",
          active ? "text-on-brand" : "text-muted-foreground"
        )}
      >
        {count}
      </span>
    </button>
  )
}
