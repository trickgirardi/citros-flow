import type { FinancialReserve, TransactionGroup } from "@/components/panels/dashboard-data"
import { FinancialReservesModal } from "@/components/board/FinancialReservesModal"
import { ImportNubankCsvModal } from "@/components/board/ImportNubankCsvModal"
import { MonthlyClosingImageModal } from "@/components/board/MonthlyClosingImageModal"
import { TransactionModal } from "@/components/modals/TransactionModal"
import { formatMoney, toCents } from "@/lib/money"
import { cn } from "@/lib/utils"

// Valores em dinheiro são strings decimais ("1234.56") e já vêm calculados pela API.
type FechamentoPanelProps = {
  balance: string
  boardName: string
  boardId: string
  canMutate: boolean
  categorySuggestions: string[]
  descriptionSuggestions: string[]
  entradaGroups: TransactionGroup[]
  entradasTotal: string
  monthLabel: string
  previousBalance: string
  reserves: FinancialReserve[]
  reservasTotal: string
  saidaGroups: TransactionGroup[]
  saidasTotal: string
  transactionCount: number
}

export function FechamentoPanel({
  balance,
  boardName,
  boardId,
  canMutate,
  categorySuggestions,
  descriptionSuggestions,
  entradaGroups,
  entradasTotal,
  monthLabel,
  previousBalance,
  reserves,
  reservasTotal,
  saidaGroups,
  saidasTotal,
  transactionCount,
}: FechamentoPanelProps) {
  const balanceIsPositive = toCents(balance) >= 0

  return (
    <aside className="flex shrink-0 flex-col rounded-lg border bg-card md:min-h-0">
      <div className="border-b px-3 py-2">
        <h2 className="text-sm font-semibold">Fechamento</h2>
        <p className="text-xs text-muted-foreground">
          {transactionCount} transacoes
        </p>
      </div>

      <div className="grid gap-2 p-3">
        <SummaryRow
          label="Saldo anterior"
          value={formatMoney(previousBalance)}
        />
        <SummaryRow
          label="Total entradas"
          value={formatMoney(entradasTotal)}
        />
        <SummaryRow label="Total saidas" value={formatMoney(saidasTotal)} />

        <div className="mt-1 flex flex-row items-center justify-between rounded-md border bg-background px-2">
          <p className="text-xs text-muted-foreground">Saldo final</p>
          <p
            className={cn(
              "mt-1 text-2xl font-semibold tabular-nums",
              balanceIsPositive
                ? "text-emerald-700 dark:text-emerald-300"
                : "text-rose-700 dark:text-rose-300"
            )}
          >
            {formatMoney(balance)}
          </p>
        </div>

        <div className="flex flex-row items-center justify-between rounded-md bg-muted px-3 py-2">
          <span className="text-xs text-muted-foreground">
            Total de reservas
          </span>
          <span className="text-sm font-semibold tabular-nums">
            {formatMoney(reservasTotal)}
          </span>
        </div>
      </div>

      {canMutate ? (
        <div className="mt-auto grid gap-2 border-t p-3">
          {/* A imagem soma ajustes temporários: recebe centavos inteiros, sem float. */}
          <MonthlyClosingImageModal
            boardName={boardName}
            entradas={toClosingCategories(entradaGroups)}
            monthLabel={monthLabel}
            previousBalance={toCents(previousBalance)}
            reservasTotal={toCents(reservasTotal)}
            saidas={toClosingCategories(saidaGroups)}
          />
          <>
            <TransactionModal
              boardId={boardId}
              categorySuggestions={categorySuggestions}
              descriptionSuggestions={descriptionSuggestions}
            />
            <ImportNubankCsvModal boardId={boardId} />
            <FinancialReservesModal boardId={boardId} reserves={reserves} />
          </>
        </div>
      ) : null}
    </aside>
  )
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-md bg-muted px-3 py-2">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-semibold tabular-nums">{value}</span>
    </div>
  )
}

function toClosingCategories(groups: TransactionGroup[]) {
  return groups.map((group) => ({
    category: group.category,
    total: toCents(group.total),
  }))
}
