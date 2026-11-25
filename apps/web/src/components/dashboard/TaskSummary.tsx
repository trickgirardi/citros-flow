import Link from "next/link"

import { StatCard } from "@/components/dashboard/StatCard"
import { DashboardSection } from "@/components/dashboard/DashboardSection"
import type { TaskStatus } from "@/components/tarefas/task-data"
import type { components } from "@/lib/api/schema"

type Tasks = components["schemas"]["DashboardTasks"]

const STATUS_LABEL: Record<TaskStatus, string> = {
  aFazer: "A fazer",
  concluida: "Concluída",
  emAndamento: "Em andamento",
}

/** Contagem por coluna e as tarefas em aberto da pessoa logada. */
export function TaskSummary({
  boardId,
  tasks,
}: {
  boardId: string
  tasks: Tasks
}) {
  const href = `/board/${boardId}/tarefas`
  const more = tasks.mineTotal - tasks.mine.length

  return (
    <DashboardSection href={href} linkLabel="Ver tarefas" title="Tarefas">
      <div className="grid grid-cols-3 gap-2">
        <StatCard label="A fazer" value={String(tasks.aFazer)} />
        <StatCard label="Em andamento" value={String(tasks.emAndamento)} />
        <StatCard label="Concluída" value={String(tasks.concluida)} />
      </div>

      <div className="grid gap-2">
        <h3 className="text-base font-bold">Minhas tarefas</h3>
        {tasks.mine.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Você ainda não tem tarefas em aberto.
          </p>
        ) : (
          <ul className="grid gap-1.5">
            {tasks.mine.map((task) => (
              <li key={task.id}>
                <Link
                  href={href}
                  className="flex items-center justify-between gap-3 rounded-field border px-3 py-2 hover:bg-accent"
                >
                  <span className="min-w-0 font-semibold [overflow-wrap:anywhere]">
                    {task.name}
                  </span>
                  <span className="shrink-0 rounded-pill bg-muted px-2.5 py-0.5 text-xs font-semibold">
                    {STATUS_LABEL[task.status]}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
        {more > 0 ? (
          <p className="text-sm text-muted-foreground">e mais {more}.</p>
        ) : null}
      </div>
    </DashboardSection>
  )
}
