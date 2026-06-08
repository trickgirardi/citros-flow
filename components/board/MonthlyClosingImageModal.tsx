"use client";

import { useState } from "react";
import { ImageDown } from "lucide-react";

import { formatCurrency } from "@/components/panels/dashboard-data";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";

export type ClosingCategorySummary = {
  category: string;
  total: number;
};

type MonthlyClosingImageModalProps = {
  boardName: string;
  entradas: ClosingCategorySummary[];
  monthLabel: string;
  previousBalance: number;
  saidas: ClosingCategorySummary[];
};

type TemporaryCategoryValue = {
  category: string;
  total: number;
};

export function MonthlyClosingImageModal({
  boardName,
  entradas,
  monthLabel,
  previousBalance,
  saidas,
}: MonthlyClosingImageModalProps) {
  const [error, setError] = useState<string | null>(null);

  function handleSubmit(formData: FormData) {
    setError(null);

    const extraEntrada = parseOptionalCategoryValue(
      formData,
      "extraEntradaCategory",
      "extraEntradaAmount"
    );
    const extraSaida = parseOptionalCategoryValue(
      formData,
      "extraSaidaCategory",
      "extraSaidaAmount"
    );
    const temporarySaidas = [
      parseFixedExpense(formData, "rentAmount", "Aluguel"),
      parseFixedExpense(formData, "waterAmount", "Água"),
      parseFixedExpense(formData, "energyAmount", "Luz"),
    ].filter((value): value is TemporaryCategoryValue => Boolean(value));

    if (extraEntrada.error || extraSaida.error) {
      setError("Preencha os valores temporarios com numeros validos.");
      return;
    }

    const adjustedEntradas = aggregateCategories(
      entradas,
      extraEntrada.value ? [extraEntrada.value] : []
    );
    const adjustedSaidas = aggregateCategories(
      saidas,
      extraSaida.value ? [...temporarySaidas, extraSaida.value] : temporarySaidas
    );
    const entradasTotal = sumCategories(adjustedEntradas);
    const saidasTotal = sumCategories(adjustedSaidas);
    const finalBalance = previousBalance + entradasTotal - saidasTotal;

    downloadClosingImage({
      boardName,
      entradas: adjustedEntradas,
      entradasTotal,
      finalBalance,
      monthLabel,
      previousBalance,
      saidas: adjustedSaidas,
      saidasTotal,
    });
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
            Inclua valores temporarios que pertencem ao mes, mas nao estao no
            dashboard.
          </DialogDescription>
        </DialogHeader>

        <form action={handleSubmit} className="grid gap-4">
          <div className="grid gap-3 md:grid-cols-3">
            <TemporaryAmountField label="Aluguel" name="rentAmount" />
            <TemporaryAmountField label="Conta de agua" name="waterAmount" />
            <TemporaryAmountField label="Conta de luz" name="energyAmount" />
          </div>

          <div className="grid gap-3 rounded-md border p-3">
            <p className="text-xs font-medium">Entrada extra</p>
            <div className="grid gap-3 sm:grid-cols-[1fr_10rem]">
              <input
                name="extraEntradaCategory"
                type="text"
                maxLength={80}
                className="h-9 rounded-md border bg-background px-3 text-sm"
                placeholder="Categoria"
              />
              <input
                name="extraEntradaAmount"
                type="number"
                inputMode="decimal"
                min="0"
                step="0.01"
                className="h-9 rounded-md border bg-background px-3 text-sm"
                placeholder="0,00"
              />
            </div>
          </div>

          <div className="grid gap-3 rounded-md border p-3">
            <p className="text-xs font-medium">Saida extra</p>
            <div className="grid gap-3 sm:grid-cols-[1fr_10rem]">
              <input
                name="extraSaidaCategory"
                type="text"
                maxLength={80}
                className="h-9 rounded-md border bg-background px-3 text-sm"
                placeholder="Categoria"
              />
              <input
                name="extraSaidaAmount"
                type="number"
                inputMode="decimal"
                min="0"
                step="0.01"
                className="h-9 rounded-md border bg-background px-3 text-sm"
                placeholder="0,00"
              />
            </div>
          </div>

          {error ? <p className="text-sm text-destructive">{error}</p> : null}

          <div className="flex justify-end">
            <Button type="submit">Baixar imagem</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function TemporaryAmountField({ label, name }: { label: string; name: string }) {
  return (
    <label className="grid gap-1.5 text-sm font-medium">
      {label}
      <input
        name={name}
        type="number"
        inputMode="decimal"
        min="0"
        step="0.01"
        className="h-9 min-w-0 rounded-md border bg-background px-3 text-sm"
        placeholder="0,00"
      />
    </label>
  );
}

function parseFixedExpense(
  formData: FormData,
  amountName: string,
  category: string
): TemporaryCategoryValue | null {
  const amount = parseAmount(formData.get(amountName)?.toString());

  return amount > 0 ? { category, total: amount } : null;
}

function parseOptionalCategoryValue(
  formData: FormData,
  categoryName: string,
  amountName: string
): { error: boolean; value: TemporaryCategoryValue | null } {
  const category = formData.get(categoryName)?.toString().trim();
  const amount = parseAmount(formData.get(amountName)?.toString());

  if (!category && amount === 0) {
    return { error: false, value: null };
  }

  if (!category || amount <= 0) {
    return { error: true, value: null };
  }

  return {
    error: false,
    value: { category, total: amount },
  };
}

function parseAmount(value: string | undefined) {
  if (!value) {
    return 0;
  }

  const amount = Number(value.replace(",", ".").trim());

  if (!Number.isFinite(amount) || amount < 0) {
    return -1;
  }

  return Math.round(amount * 100) / 100;
}

function aggregateCategories(
  baseCategories: ClosingCategorySummary[],
  temporaryCategories: TemporaryCategoryValue[]
) {
  const categories = new Map<string, number>();

  [...baseCategories, ...temporaryCategories].forEach((item) => {
    const category = item.category.trim();

    if (!category || item.total <= 0) {
      return;
    }

    categories.set(category, (categories.get(category) ?? 0) + item.total);
  });

  return Array.from(categories.entries())
    .map(([category, total]) => ({ category, total }))
    .sort((first, second) => first.category.localeCompare(second.category, "pt-BR"));
}

function sumCategories(categories: ClosingCategorySummary[]) {
  return categories.reduce((total, category) => total + category.total, 0);
}

function downloadClosingImage({
  boardName,
  entradas,
  entradasTotal,
  finalBalance,
  monthLabel,
  previousBalance,
  saidas,
  saidasTotal,
}: {
  boardName: string;
  entradas: ClosingCategorySummary[];
  entradasTotal: number;
  finalBalance: number;
  monthLabel: string;
  previousBalance: number;
  saidas: ClosingCategorySummary[];
  saidasTotal: number;
}) {
  const width = 1400;
  const height = 1400;
  const padding = 78;
  const contentTop = 265;
  const canvas = document.createElement("canvas");
  const context = canvas.getContext("2d");

  if (!context) {
    return;
  }

  canvas.width = width;
  canvas.height = height;

  context.fillStyle = "#fff7ed";
  context.fillRect(0, 0, width, height);

  drawDecorations(context, width);
  drawText(context, "Fluxo de caixa", width / 2, 92, "bold 62px Arial", "#142554", "center");
  drawText(context, boardName, width / 2, 152, "bold 34px Arial", "#142554", "center");
  drawText(context, capitalize(monthLabel), width / 2, 198, "26px Arial", "#475569", "center");

  const columnWidth = (width - padding * 2 - 42) / 2;
  const cardHeight = 640;
  const leftX = padding;
  const rightX = padding + columnWidth + 42;
  const finalTop = contentTop + cardHeight + 70;

  drawCategoryCard(context, {
    accent: "#fb9473",
    categories: entradas,
    icon: "💰",
    title: "RESUMO DE ENTRADAS",
    totalLabel: "TOTAL ENTRADAS",
    x: leftX,
    y: contentTop,
    width: columnWidth,
    height: cardHeight,
  });
  drawCategoryCard(context, {
    accent: "#162857",
    categories: saidas,
    icon: "🧾",
    sign: "-",
    title: "RESUMO DE SAÍDAS",
    totalLabel: "TOTAL SAÍDAS",
    x: rightX,
    y: contentTop,
    width: columnWidth,
    height: cardHeight,
  });
  drawFinalBalanceCard(context, {
    entradasTotal,
    finalBalance,
    previousBalance,
    saidasTotal,
    x: padding,
    y: finalTop,
    width,
  });

  canvas.toBlob((blob) => {
    if (!blob) {
      return;
    }

    const link = document.createElement("a");
    const url = URL.createObjectURL(blob);

    link.href = url;
    link.download = `fechamento-${slugify(boardName)}-${slugify(monthLabel)}.png`;
    link.click();
    URL.revokeObjectURL(url);
  }, "image/png");
}

function drawCategoryCard(
  context: CanvasRenderingContext2D,
  {
    accent,
    categories,
    height,
    icon,
    sign = "",
    title,
    totalLabel,
    width,
    x,
    y,
  }: {
    accent: string;
    categories: ClosingCategorySummary[];
    height: number;
    icon: string;
    sign?: string;
    title: string;
    totalLabel: string;
    width: number;
    x: number;
    y: number;
  }
) {
  const headerHeight = 132;
  const totalY = y + height - 168;
  const total = sumCategories(categories);

  fillRoundRect(context, x, y, width, height, 28, "#ffffff");
  strokeRoundRect(context, x, y, width, height, 28, accent);
  fillRoundRect(context, x, y, width, headerHeight, 28, accent);
  context.fillStyle = accent;
  context.fillRect(x, y + headerHeight - 28, width, 28);

  drawText(context, icon, x + 44, y + 82, "54px Arial", "#111827");
  drawText(context, title, x + 128, y + 58, "bold 34px Arial", "#ffffff");
  drawText(context, "", x + 128, y + 98, "bold 34px Arial", "#ffffff");

  if (categories.length === 0) {
    drawText(context, "Sem lançamentos", x + 42, y + 205, "28px Arial", "#64748b");
  }

  categories.slice(0, 8).forEach((category, index) => {
    const rowY = y + 190 + index * 48;

    drawText(context, truncate(category.category, 24), x + 42, rowY, "28px Arial");
    drawText(
      context,
      `${sign}${formatCurrency(category.total)}`,
      x + width - 42,
      rowY,
      "28px Arial",
      "#111827",
      "right"
    );

    drawLine(context, x + 42, rowY + 20, x + width - 42, rowY + 20, "#e5e7eb");
  });

  context.fillStyle = `${accent}22`;
  context.fillRect(x, totalY, width, 78);
  drawText(context, totalLabel, x + 42, totalY + 50, "bold 32px Arial", "#111827");
  drawText(
    context,
    `${sign}${formatCurrency(total)}`,
    x + width - 42,
    totalY + 50,
    "bold 32px Arial",
    "#111827",
    "right"
  );
}

function drawFinalBalanceCard(
  context: CanvasRenderingContext2D,
  {
    entradasTotal,
    finalBalance,
    previousBalance,
    saidasTotal,
    x,
    y,
    width,
  }: {
    entradasTotal: number;
    finalBalance: number;
    previousBalance: number;
    saidasTotal: number;
    x: number;
    y: number;
    width: number;
  }
) {
  const cardWidth = width - x * 2;
  const headerHeight = 92;
  const balanceColor = finalBalance >= 0 ? "#fb9473" : "#b91c1c";

  fillRoundRect(context, x, y, cardWidth, 300, 28, "#ffffff");
  strokeRoundRect(context, x, y, cardWidth, 300, 28, "#162857");
  fillRoundRect(context, x, y, cardWidth, headerHeight, 28, "#162857");
  context.fillStyle = "#162857";
  context.fillRect(x, y + headerHeight - 28, cardWidth, 28);

  drawText(
    context,
    "SALDO FINAL DO MÊS",
    width / 2,
    y + 58,
    "bold 40px Arial",
    "#ffffff",
    "center"
  );
  drawText(
    context,
    `Saldo anterior (${formatCurrency(previousBalance)}) + Entradas (${formatCurrency(
      entradasTotal
    )}) - Saídas (${formatCurrency(saidasTotal)})`,
    width / 2,
    y + 150,
    "30px Arial",
    "#111827",
    "center"
  );
  drawText(
    context,
    formatCurrency(finalBalance),
    width / 2,
    y + 245,
    "bold 76px Arial",
    balanceColor,
    "center"
  );
}

function drawText(
  context: CanvasRenderingContext2D,
  text: string,
  x: number,
  y: number,
  font: string,
  color = "#111827",
  align: CanvasTextAlign = "left"
) {
  context.font = font;
  context.fillStyle = color;
  context.textAlign = align;
  context.fillText(text, x, y);
}

function drawLine(
  context: CanvasRenderingContext2D,
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
  color: string
) {
  context.strokeStyle = color;
  context.lineWidth = 2;
  context.beginPath();
  context.moveTo(fromX, fromY);
  context.lineTo(toX, toY);
  context.stroke();
}

function drawDecorations(context: CanvasRenderingContext2D, width: number) {
  context.globalAlpha = 0.18;
  drawText(context, "$", 78, 88, "bold 64px Arial", "#162857", "center");
  drawText(context, "↗", width - 90, 145, "bold 70px Arial", "#fb9473", "center");
  drawText(context, "●", width - 170, 78, "bold 42px Arial", "#fb9473", "center");
  context.globalAlpha = 1;
}

function fillRoundRect(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number,
  color: string
) {
  context.fillStyle = color;
  roundedRectPath(context, x, y, width, height, radius);
  context.fill();
}

function strokeRoundRect(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number,
  color: string
) {
  context.strokeStyle = color;
  context.lineWidth = 4;
  roundedRectPath(context, x, y, width, height, radius);
  context.stroke();
}

function roundedRectPath(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number
) {
  context.beginPath();
  context.moveTo(x + radius, y);
  context.lineTo(x + width - radius, y);
  context.quadraticCurveTo(x + width, y, x + width, y + radius);
  context.lineTo(x + width, y + height - radius);
  context.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
  context.lineTo(x + radius, y + height);
  context.quadraticCurveTo(x, y + height, x, y + height - radius);
  context.lineTo(x, y + radius);
  context.quadraticCurveTo(x, y, x + radius, y);
  context.closePath();
}

function truncate(value: string, maxLength: number) {
  return value.length > maxLength ? `${value.slice(0, maxLength - 1)}…` : value;
}

function capitalize(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}

function slugify(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)/g, "");
}
