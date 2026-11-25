import { Flag } from "lucide-react"

import { BADGE_CLASS } from "@/components/estoque/StockStatusBadge"
import { priorityInfo, type TaskPriority } from "@/components/tarefas/task-data"
import { cn } from "@/lib/utils"

/** Selo da prioridade: bandeira + texto + cor (a cor nunca é a única pista; a bandeira o separa das tags). */
export function TaskPriorityBadge({ priority }: { priority: TaskPriority }) {
  const info = priorityInfo(priority)

  return (
    <span className={cn(BADGE_CLASS, "gap-1", info.style)}>
      <Flag aria-hidden="true" className="size-3" />
      {info.label}
    </span>
  )
}
