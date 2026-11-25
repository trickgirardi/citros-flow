import { formatMoney, toCents } from "@/lib/money"
import { cn } from "@/lib/utils"
import type { components } from "@/lib/api/schema"

type Month = components["schemas"]["DashboardMonth"]

const monthFormatter = new Intl.DateTimeFormat("pt-BR", {
  month: "short",
  timeZone: "UTC",
})
const longMonthFormatter = new Intl.DateTimeFormat("pt-BR", {
  month: "long",
  timeZone: "UTC",
  year: "numeric",
})

/** "2026-11" -> a data no dia 15 (UTC), para formatar o mês sem efeito de fuso. */
function monthDate(month: string) {
  const [year, number] = month.split("-").map(Number)

  return new Date(Date.UTC(year, number - 1, 15))
}

type CashChartProps = {
  history: readonly Month[]
  /** O mês atual (o último), em destaque. */
  currentMonth: string
}

/**
 * Colunas de entradas (azul) e saídas (vermelho) por mês, em CSS puro (sem biblioteca). A escala é a
 * do maior valor da série. Tabela equivalente só para leitor de tela (WCAG 1.1.1).
 */
export function CashChart({ currentMonth, history }: CashChartProps) {
  const max = Math.max(
    ...history.flatMap((month) => [
      toCents(month.entradas),
      toCents(month.saidas),
    ])
  )

  if (max === 0) {
    return (
      <p className="rounded-field border border-dashed p-6 text-center text-sm text-muted-foreground">
        Sem lançamentos nos últimos {history.length} meses.
      </p>
    )
  }

  const heightOf = (value: string) => {
    const cents = toCents(value)

    // Um valor pequeno ainda aparece (ao menos 2%); zero não desenha coluna.
    return cents === 0 ? 0 : Math.max(2, Math.round((cents / max) * 100))
  }

  return (
    <figure className="grid gap-2">
      <figcaption className="flex flex-wrap items-center justify-between gap-2 text-sm">
        <span className="font-semibold">
          Entradas e saídas nos últimos {history.length} meses
        </span>
        <span className="flex items-center gap-3 text-muted-foreground">
          <span className="flex items-center gap-1.5">
            <span
              aria-hidden="true"
              className="size-3 rounded-sm bg-accent-ink"
            />
            Entradas
          </span>
          <span className="flex items-center gap-1.5">
            <span aria-hidden="true" className="size-3 rounded-sm bg-danger" />
            Saídas
          </span>
        </span>
      </figcaption>

      <div aria-hidden="true" className="grid grid-cols-[auto_1fr] gap-2">
        <div className="flex h-44 flex-col justify-between text-right text-xs text-muted-foreground tabular-nums">
          <span>{formatMoney((max / 100).toFixed(2))}</span>
          <span>R$ 0,00</span>
        </div>
        <ol className="grid h-44 auto-cols-fr grid-flow-col gap-1.5 border-b border-l pl-1.5">
          {history.map((month) => {
            const current = month.month === currentMonth

            return (
              <li
                key={month.month}
                title={`${longMonthFormatter.format(monthDate(month.month))}: entradas ${formatMoney(month.entradas)}, saídas ${formatMoney(month.saidas)}`}
                className={cn(
                  "flex items-end justify-center gap-1 rounded-t-lg px-1",
                  current && "bg-accent"
                )}
              >
                <span
                  className="w-full max-w-6 rounded-t-sm bg-accent-ink"
                  style={{ height: `${heightOf(month.entradas)}%` }}
                />
                <span
                  className="w-full max-w-6 rounded-t-sm bg-danger"
                  style={{ height: `${heightOf(month.saidas)}%` }}
                />
              </li>
            )
          })}
        </ol>
        <span />
        <ol className="grid auto-cols-fr grid-flow-col gap-1.5 pl-1.5 text-center text-xs text-muted-foreground">
          {history.map((month) => (
            <li
              key={month.month}
              className={cn(
                month.month === currentMonth && "font-bold text-foreground"
              )}
            >
              {monthFormatter.format(monthDate(month.month))}
            </li>
          ))}
        </ol>
      </div>

      <table className="sr-only">
        <caption>Entradas e saídas por mês</caption>
        <thead>
          <tr>
            <th scope="col">Mês</th>
            <th scope="col">Entradas</th>
            <th scope="col">Saídas</th>
          </tr>
        </thead>
        <tbody>
          {history.map((month) => (
            <tr key={month.month}>
              <th scope="row">
                {longMonthFormatter.format(monthDate(month.month))}
              </th>
              <td>{formatMoney(month.entradas)}</td>
              <td>{formatMoney(month.saidas)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}
