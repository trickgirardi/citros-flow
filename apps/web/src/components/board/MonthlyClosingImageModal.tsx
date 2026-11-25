"use client"

import { useState } from "react"
import { ImageDown } from "lucide-react"

import { getClosingPeriod } from "@/app/(app)/board/[boardId]/actions"
import {
  downloadClosingImage,
  sumCategories,
  type ClosingCategorySummary,
} from "@/components/board/closing-image"
import type { TransactionGroup } from "@/components/panels/dashboard-data"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { normalizeMoneyInput, toCents } from "@/lib/money"

// periodStart/periodEnd (yyyy-MM-dd, inclusivos) são o período padrão: o mês exibido no dashboard.
type MonthlyClosingImageModalProps = {
  boardId: string
  boardName: string
  monthLabel: string
  periodEnd: string
  periodStart: string
}

type TemporaryCategoryValue = {
  category: string
  total: number
}

export function MonthlyClosingImageModal({
  boardId,
  boardName,
  monthLabel,
  periodEnd,
  periodStart,
}: MonthlyClosingImageModalProps) {
  const [error, setError] = useState<string | null>(null)
  const [isPending, setIsPending] = useState(false)

  async function handleSubmit(formData: FormData) {
    setError(null)

    const start = formData.get("periodStart")?.toString() ?? ""
    const end = formData.get("periodEnd")?.toString() ?? ""

    if (!start || !end) {
      setError("Informe a data de inicio e a data de fim.")
      return
    }

    // yyyy-MM-dd compara certo como texto.
    if (end < start) {
      setError("A data de fim nao pode ser anterior a data de inicio.")
      return
    }

    const extraEntrada = parseOptionalCategoryValue(
      formData,
      "extraEntradaCategory",
      "extraEntradaAmount"
    )
    const extraSaida = parseOptionalCategoryValue(
      formData,
      "extraSaidaCategory",
      "extraSaidaAmount"
    )

    if (extraEntrada.error || extraSaida.error) {
      setError("Preencha os valores temporarios com numeros validos.")
      return
    }

    // Entradas, saídas, saldo anterior e reservas do período vêm da API; aqui só entram os ajustes.
    setIsPending(true)
    let result: Awaited<ReturnType<typeof getClosingPeriod>>
    try {
      result = await getClosingPeriod(boardId, start, end)
    } finally {
      setIsPending(false)
    }

    if (result.summary === null) {
      setError(result.error)
      return
    }

    const { summary } = result
    const previousBalance = toCents(summary.previousBalance)
    const adjustedEntradas = aggregateCategories(
      toClosingCategories(summary.entradas),
      extraEntrada.value ? [extraEntrada.value] : []
    )
    const adjustedSaidas = aggregateCategories(
      toClosingCategories(summary.saidas),
      extraSaida.value ? [extraSaida.value] : []
    )
    const entradasTotal = sumCategories(adjustedEntradas)
    const saidasTotal = sumCategories(adjustedSaidas)
    const finalBalance = previousBalance + entradasTotal - saidasTotal
    const isWholeMonth = start === periodStart && end === periodEnd

    await downloadClosingImage({
      boardName,
      entradas: adjustedEntradas,
      entradasTotal,
      finalBalance,
      periodLabel: isWholeMonth
        ? capitalize(monthLabel)
        : `${capitalize(monthLabel)} (${formatDate(start)} a ${formatDate(end)})`,
      previousBalance,
      reservasTotal: toCents(summary.reservesTotal),
      saidas: adjustedSaidas,
      saidasTotal,
    })
  }

  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button type="button" variant="outline" className="h-9 w-full">
          <ImageDown data-icon="inline-start" />
          Gerar fechamento
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Gerar fechamento</DialogTitle>
          <DialogDescription>
            Escolha o periodo do fechamento e inclua valores temporarios que
            pertencem a ele, mas nao estao no dashboard.
          </DialogDescription>
        </DialogHeader>

        <form action={handleSubmit} className="grid gap-4">
          <div className="grid gap-3 rounded-field border p-4">
            <p className="text-sm font-semibold">Periodo do fechamento</p>
            <div className="grid gap-3 sm:grid-cols-2">
              <label className="grid gap-1.5 text-sm font-semibold">
                Inicio
                <Input
                  name="periodStart"
                  type="date"
                  required
                  defaultValue={periodStart}
                />
              </label>
              <label className="grid gap-1.5 text-sm font-semibold">
                Fim (inclusive)
                <Input
                  name="periodEnd"
                  type="date"
                  required
                  defaultValue={periodEnd}
                />
              </label>
            </div>
            <p className="text-sm text-muted-foreground">
              Para incluir contas pagas no inicio do mes seguinte, termine no
              dia do pagamento.
            </p>
          </div>

          <div className="grid gap-3 rounded-field border p-4">
            <p className="text-sm font-semibold">Entrada extra</p>
            <div className="grid gap-3 sm:grid-cols-[1fr_10rem]">
              <Input
                name="extraEntradaCategory"
                type="text"
                maxLength={80}
                placeholder="Categoria"
              />
              <Input
                name="extraEntradaAmount"
                type="number"
                inputMode="decimal"
                min="0"
                step="0.01"
                placeholder="0,00"
              />
            </div>
          </div>

          <div className="grid gap-3 rounded-field border p-4">
            <p className="text-sm font-semibold">Saida extra</p>
            <div className="grid gap-3 sm:grid-cols-[1fr_10rem]">
              <Input
                name="extraSaidaCategory"
                type="text"
                maxLength={80}
                placeholder="Categoria"
              />
              <Input
                name="extraSaidaAmount"
                type="number"
                inputMode="decimal"
                min="0"
                step="0.01"
                placeholder="0,00"
              />
            </div>
          </div>

          {error ? (
            <p role="alert" className="text-sm font-semibold text-destructive">
              {error}
            </p>
          ) : null}

          <div className="flex justify-end">
            <Button type="submit" size="lg" disabled={isPending}>
              {isPending ? "Gerando..." : "Baixar imagem"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function parseOptionalCategoryValue(
  formData: FormData,
  categoryName: string,
  amountName: string
): { error: boolean; value: TemporaryCategoryValue | null } {
  const category = formData.get(categoryName)?.toString().trim()
  const amount = parseAmount(formData.get(amountName)?.toString())

  if (!category && amount === 0) {
    return { error: false, value: null }
  }

  if (!category || amount <= 0) {
    return { error: true, value: null }
  }

  return {
    error: false,
    value: { category, total: amount },
  }
}

/** Centavos digitados: 0 se vazio, -1 se inválido. Aceita "10,50", "10.50" e "1.234,56". */
function parseAmount(value: string | undefined) {
  if (!value?.trim()) {
    return 0
  }

  const normalized = normalizeMoneyInput(value)

  return normalized === null ? -1 : toCents(normalized)
}

function toClosingCategories(groups: TransactionGroup[]): ClosingCategorySummary[] {
  return groups.map((group) => ({
    category: group.category,
    total: toCents(group.total),
  }))
}

/** yyyy-MM-dd -> dd/MM/yyyy, sem passar por Date (sem efeito de fuso). */
function formatDate(value: string) {
  return value.split("-").reverse().join("/")
}

function aggregateCategories(
  baseCategories: ClosingCategorySummary[],
  temporaryCategories: TemporaryCategoryValue[]
) {
  const categories = new Map<string, number>()

  ;[...baseCategories, ...temporaryCategories].forEach((item) => {
    const category = item.category.trim()

    if (!category || item.total <= 0) {
      return
    }

    categories.set(category, (categories.get(category) ?? 0) + item.total)
  })

  return Array.from(categories.entries())
    .map(([category, total]) => ({ category, total }))
    .sort((first, second) =>
      first.category.localeCompare(second.category, "pt-BR")
    )
}

function capitalize(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1)
}
