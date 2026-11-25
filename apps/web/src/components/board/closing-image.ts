import { formatCents } from "@/lib/money"

// Todos os valores aqui são em CENTAVOS (inteiros): a soma dos ajustes temporários não pode usar float.
export type ClosingCategorySummary = {
  category: string
  total: number
}

type ClosingImage = {
  boardName: string
  entradas: ClosingCategorySummary[]
  entradasTotal: number
  finalBalance: number
  periodLabel: string
  previousBalance: number
  reservasTotal: number
  saidas: ClosingCategorySummary[]
  saidasTotal: number
}

// Cores e fontes vêm dos tokens do CSS (styles/tokens.css e globals.css): a imagem é sempre clara,
// qualquer que seja o tema da tela, então usa a paleta fixa `--igb-*` e não os tokens de tema.
type Brand = {
  blue: string
  yellow: string
  red: string
  cream: string
  white: string
  ink: string
  muted: string
  border: string
  sans: string
  heading: string
}

const WIDTH = 1400
const HEIGHT = 1400
const PADDING = 78
const HEADER_HEIGHT = 226
const CONTENT_TOP = 275
const RADIUS = 24

export function sumCategories(categories: ClosingCategorySummary[]) {
  return categories.reduce((total, category) => total + category.total, 0)
}

function readBrand(): Brand {
  const style = getComputedStyle(document.documentElement)
  const read = (name: string) => style.getPropertyValue(name).trim()

  return {
    blue: read("--igb-blue"),
    yellow: read("--igb-yellow"),
    red: read("--igb-red"),
    cream: read("--igb-cream"),
    white: read("--igb-white"),
    ink: read("--igb-ink"),
    muted: read("--igb-ink-muted"),
    border: read("--igb-border"),
    sans: read("--font-sans"),
    heading: read("--font-heading"),
  }
}

/** O Sol de Aquário; sem ele a imagem sai igual, só sem o símbolo. */
function loadImage(src: string) {
  return new Promise<HTMLImageElement | null>((resolve) => {
    const image = new Image()

    image.onload = () => resolve(image)
    image.onerror = () => resolve(null)
    image.src = src
  })
}

/** Fraunces (título): carrega antes de desenhar; se falhar, cai na serifa do sistema. */
async function loadHeadingFont(heading: string) {
  try {
    await document.fonts.load(`600 64px ${heading}`)
  } catch {
    // segue com a fonte de reserva
  }
}

export async function downloadClosingImage({
  boardName,
  entradas,
  entradasTotal,
  finalBalance,
  periodLabel,
  previousBalance,
  reservasTotal,
  saidas,
  saidasTotal,
}: ClosingImage) {
  const canvas = document.createElement("canvas")
  const context = canvas.getContext("2d")

  if (!context) {
    return
  }

  const brand = readBrand()
  const [symbol] = await Promise.all([
    loadImage("/brand/sol-de-aquario-192.png"),
    loadHeadingFont(brand.heading),
  ])

  canvas.width = WIDTH
  canvas.height = HEIGHT

  context.fillStyle = brand.cream
  context.fillRect(0, 0, WIDTH, HEIGHT)

  drawHeader(context, brand, { boardName, periodLabel, symbol })

  const columnWidth = (WIDTH - PADDING * 2 - 42) / 2
  const cardHeight = 640
  const finalTop = CONTENT_TOP + cardHeight + 70

  drawCategoryCard(context, brand, {
    accent: brand.blue,
    categories: entradas,
    title: "RESUMO DE ENTRADAS",
    totalLabel: "TOTAL ENTRADAS",
    x: PADDING,
    y: CONTENT_TOP,
    width: columnWidth,
    height: cardHeight,
  })
  drawCategoryCard(context, brand, {
    accent: brand.red,
    categories: saidas,
    sign: "-",
    title: "RESUMO DE SAÍDAS",
    totalLabel: "TOTAL SAÍDAS",
    x: PADDING + columnWidth + 42,
    y: CONTENT_TOP,
    width: columnWidth,
    height: cardHeight,
  })
  drawFinalBalanceCard(context, brand, {
    entradasTotal,
    finalBalance,
    previousBalance,
    reservasTotal,
    saidasTotal,
    y: finalTop,
  })

  canvas.toBlob((blob) => {
    if (!blob) {
      return
    }

    const link = document.createElement("a")
    const url = URL.createObjectURL(blob)

    link.href = url
    link.download = `fechamento-${slugify(boardName)}-${slugify(periodLabel)}.png`
    link.click()
    URL.revokeObjectURL(url)
  }, "image/png")
}

/** Faixa azul da marca: Sol de Aquário em círculo branco, título em Fraunces, board e período. */
function drawHeader(
  context: CanvasRenderingContext2D,
  brand: Brand,
  {
    boardName,
    periodLabel,
    symbol,
  }: { boardName: string; periodLabel: string; symbol: HTMLImageElement | null }
) {
  context.fillStyle = brand.blue
  context.fillRect(0, 0, WIDTH, HEADER_HEIGHT)
  context.fillStyle = brand.yellow
  context.fillRect(0, HEADER_HEIGHT, WIDTH, 8)

  const centerX = PADDING + 60
  const centerY = HEADER_HEIGHT / 2

  context.fillStyle = brand.white
  context.beginPath()
  context.arc(centerX, centerY, 60, 0, Math.PI * 2)
  context.fill()

  if (symbol) {
    context.drawImage(symbol, centerX - 48, centerY - 48, 96, 96)
  }

  const textX = PADDING + 154

  drawText(context, "Fluxo de caixa", textX, 100, `600 64px ${brand.heading}`, brand.white)
  drawText(context, truncate(boardName, 46), textX, 150, `bold 34px ${brand.sans}`, brand.white)
  drawText(context, periodLabel, textX, 192, `26px ${brand.sans}`, brand.cream)
}

function drawCategoryCard(
  context: CanvasRenderingContext2D,
  brand: Brand,
  {
    accent,
    categories,
    height,
    sign = "",
    title,
    totalLabel,
    width,
    x,
    y,
  }: {
    accent: string
    categories: ClosingCategorySummary[]
    height: number
    sign?: string
    title: string
    totalLabel: string
    width: number
    x: number
    y: number
  }
) {
  const headerHeight = 132
  const totalHeight = 78
  const totalY = y + height - totalHeight
  const listTop = y + 190
  const rowHeight = 48
  const listBottom = totalY - 28
  const maxVisibleRows = Math.max(0, Math.floor((listBottom - listTop) / rowHeight))
  const visibleCategories = categories.slice(0, maxVisibleRows)
  const hiddenCount = categories.length - visibleCategories.length
  const total = sumCategories(categories)

  drawCard(context, brand, { x, y, width, height })

  // cabeçalho colorido (cantos de baixo retos)
  fillRoundRect(context, x, y, width, headerHeight, RADIUS, accent)
  context.fillStyle = accent
  context.fillRect(x, y + headerHeight - RADIUS, width, RADIUS)
  drawText(context, title, x + 42, y + 82, `bold 34px ${brand.sans}`, brand.white)

  if (categories.length === 0) {
    drawText(context, "Sem lançamentos", x + 42, y + 205, `28px ${brand.sans}`, brand.muted)
  }

  visibleCategories.forEach((category, index) => {
    const rowY = listTop + index * rowHeight

    drawText(context, truncate(category.category, 24), x + 42, rowY, `28px ${brand.sans}`, brand.ink)
    drawText(
      context,
      `${sign}${formatCents(category.total)}`,
      x + width - 42,
      rowY,
      `28px ${brand.sans}`,
      brand.ink,
      "right"
    )
    drawLine(context, x + 42, rowY + 20, x + width - 42, rowY + 20, brand.border)
  })

  if (hiddenCount > 0) {
    drawText(
      context,
      `+ ${hiddenCount} categoria(s)`,
      x + 42,
      totalY - 22,
      `22px ${brand.sans}`,
      brand.muted
    )
  }

  // faixa do total, recortada nos cantos arredondados do card
  context.save()
  roundedRectPath(context, x, y, width, height, RADIUS)
  context.clip()
  context.fillStyle = `${accent}1a`
  context.fillRect(x, totalY, width, totalHeight)
  context.restore()

  drawText(context, totalLabel, x + 42, totalY + 50, `bold 32px ${brand.sans}`, brand.ink)
  drawText(
    context,
    `${sign}${formatCents(total)}`,
    x + width - 42,
    totalY + 50,
    `bold 32px ${brand.sans}`,
    brand.ink,
    "right"
  )
}

function drawFinalBalanceCard(
  context: CanvasRenderingContext2D,
  brand: Brand,
  {
    entradasTotal,
    finalBalance,
    previousBalance,
    reservasTotal,
    saidasTotal,
    y,
  }: {
    entradasTotal: number
    finalBalance: number
    previousBalance: number
    reservasTotal: number
    saidasTotal: number
    y: number
  }
) {
  const x = PADDING
  const cardWidth = WIDTH - PADDING * 2
  const cardHeight = 350
  const headerHeight = 92
  const centerX = WIDTH / 2

  drawCard(context, brand, { x, y, width: cardWidth, height: cardHeight })

  fillRoundRect(context, x, y, cardWidth, headerHeight, RADIUS, brand.blue)
  context.fillStyle = brand.blue
  context.fillRect(x, y + headerHeight - RADIUS, cardWidth, RADIUS)
  drawText(context, "SALDO FINAL", centerX, y + 58, `bold 40px ${brand.sans}`, brand.white, "center")

  drawText(
    context,
    `Saldo anterior (${formatCents(previousBalance)}) + Entradas (${formatCents(
      entradasTotal
    )}) - Saídas (${formatCents(saidasTotal)})`,
    centerX,
    y + 150,
    `30px ${brand.sans}`,
    brand.muted,
    "center"
  )

  // Destaque do saldo: amarelo (texto escuro, 10,9:1) quando positivo; vermelho (texto branco,
  // 4,97:1) quando negativo. O sinal "-" também vem no texto: não depende só da cor.
  const amount = formatCents(finalBalance)
  const positive = finalBalance >= 0

  context.font = `bold 76px ${brand.sans}`
  const pillWidth = context.measureText(amount).width + 96

  fillRoundRect(context, centerX - pillWidth / 2, y + 172, pillWidth, 100, 50, positive ? brand.yellow : brand.red)
  drawText(
    context,
    amount,
    centerX,
    y + 249,
    `bold 76px ${brand.sans}`,
    positive ? brand.ink : brand.white,
    "center"
  )

  drawText(
    context,
    `Reservas financeiras: ${formatCents(reservasTotal)}`,
    centerX,
    y + 316,
    `26px ${brand.sans}`,
    brand.muted,
    "center"
  )
}

/** Card branco de 24px com a sombra azulada da marca e borda de 2px. */
function drawCard(
  context: CanvasRenderingContext2D,
  brand: Brand,
  { x, y, width, height }: { x: number; y: number; width: number; height: number }
) {
  context.save()
  context.shadowColor = `${brand.blue}24`
  context.shadowBlur = 30
  context.shadowOffsetY = 10
  fillRoundRect(context, x, y, width, height, RADIUS, brand.white)
  context.restore()

  context.strokeStyle = brand.border
  context.lineWidth = 2
  roundedRectPath(context, x, y, width, height, RADIUS)
  context.stroke()
}

function drawText(
  context: CanvasRenderingContext2D,
  text: string,
  x: number,
  y: number,
  font: string,
  color: string,
  align: CanvasTextAlign = "left"
) {
  context.font = font
  context.fillStyle = color
  context.textAlign = align
  context.fillText(text, x, y)
}

function drawLine(
  context: CanvasRenderingContext2D,
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
  color: string
) {
  context.strokeStyle = color
  context.lineWidth = 2
  context.beginPath()
  context.moveTo(fromX, fromY)
  context.lineTo(toX, toY)
  context.stroke()
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
  context.fillStyle = color
  roundedRectPath(context, x, y, width, height, radius)
  context.fill()
}

function roundedRectPath(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number
) {
  context.beginPath()
  context.roundRect(x, y, width, height, radius)
}

function truncate(value: string, maxLength: number) {
  return value.length > maxLength ? `${value.slice(0, maxLength - 1)}…` : value
}

function slugify(value: string) {
  return value
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)/g, "")
}
