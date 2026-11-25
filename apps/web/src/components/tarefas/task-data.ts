import type { components } from "@/lib/api/schema"

export type Task = components["schemas"]["BoardTaskDto"]
export type TaskStatus = components["schemas"]["BoardTaskStatus"]
export type TaskAssignee = components["schemas"]["TaskAssigneeDto"]

/** Colunas fixas do quadro, na ordem da tela. */
export const TASK_COLUMNS: readonly { label: string; status: TaskStatus }[] = [
  { label: "A fazer", status: "aFazer" },
  { label: "Em andamento", status: "emAndamento" },
  { label: "Concluída", status: "concluida" },
]

export const TASK_LIMITS = {
  description: 2000,
  name: 120,
  tag: 30,
  tags: 10,
} as const

export type TaskMove = { id: string; index: number; status: TaskStatus }

/** As tarefas de uma coluna, na ordem do quadro. */
export function columnOf(tasks: readonly Task[], status: TaskStatus) {
  return tasks
    .filter((task) => task.status === status)
    .sort((first, second) => first.position - second.position)
}

/**
 * Aplica um movimento na lista, como a API fará: a tarefa sai da coluna de origem e entra na de
 * destino em `index` (além do fim vai para o fim); as duas colunas ficam com posições 0..n-1.
 * Usada no arrastar otimista: o cartão muda na hora, antes da resposta da API.
 */
export function moveTask(tasks: readonly Task[], move: TaskMove): Task[] {
  const moving = tasks.find((task) => task.id === move.id)

  if (!moving) {
    return [...tasks]
  }

  const target = columnOf(tasks, move.status).filter(
    (task) => task.id !== move.id
  )

  target.splice(Math.min(move.index, target.length), 0, {
    ...moving,
    status: move.status,
  })

  const renumber = (column: Task[]) =>
    column.map((task, position) => ({ ...task, position }))
  const touched = new Set([moving.status, move.status])
  const untouched = tasks.filter((task) => !touched.has(task.status))
  const origin =
    moving.status === move.status
      ? []
      : columnOf(tasks, moving.status).filter((task) => task.id !== move.id)

  return [...untouched, ...renumber(origin), ...renumber(target)]
}

/** Tags já usadas no board, sem repetir (sem distinguir maiúsculas), para sugerir ao digitar. */
export function tagSuggestions(tasks: readonly Task[]) {
  const seen = new Map<string, string>()

  for (const task of tasks) {
    for (const tag of task.tags) {
      if (!seen.has(tag.toLowerCase())) {
        seen.set(tag.toLowerCase(), tag)
      }
    }
  }

  return [...seen.values()].sort((first, second) =>
    first.localeCompare(second, "pt-BR")
  )
}

// A cor de cada tag sai do texto (sem cadastro de tags): a mesma tag tem sempre a mesma cor.
const TAG_STYLES = [
  "bg-accent-ink/10 text-accent-ink",
  "bg-primary/30 text-foreground",
  "bg-destructive/10 text-destructive",
  "bg-muted text-muted-foreground",
]

export function tagStyle(tag: string) {
  let hash = 0

  for (const char of tag.toLowerCase()) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0
  }

  return TAG_STYLES[hash % TAG_STYLES.length]
}
