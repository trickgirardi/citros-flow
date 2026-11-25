import type { components } from "@/lib/api/schema"

export type AdminBoard = components["schemas"]["BoardAdminResponse"]

function count(value: number, singular: string, plural: string) {
  return `${value} ${value === 1 ? singular : plural}`
}

/** "12 transacoes, 1 reserva, 0 links publicos": o que o board guarda (e o que some se for removido). */
export function describeBoardContents(board: AdminBoard) {
  return [
    count(board.transactionCount, "transacao", "transacoes"),
    count(board.reserveCount, "reserva", "reservas"),
    count(board.shareLinkCount, "link publico", "links publicos"),
  ].join(", ")
}
