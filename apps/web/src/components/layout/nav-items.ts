import {
  Boxes,
  LayoutDashboard,
  ListChecks,
  Wallet,
  type LucideIcon,
} from "lucide-react"

/** Módulo de um board: `segment` é o final do endereço (/board/{id}/{segment}). */
export type BoardModule = {
  icon: LucideIcon
  label: string
  segment: string
  /** Ainda não existe: o item mostra o selo "Em breve" e abre a página "Em construção". */
  soon: boolean
}

export const BOARD_MODULES: readonly BoardModule[] = [
  {
    icon: LayoutDashboard,
    label: "Dashboard",
    segment: "dashboard",
    soon: false,
  },
  {
    icon: Wallet,
    label: "Caixa",
    segment: "fluxo-de-caixa",
    soon: false,
  },
  {
    icon: ListChecks,
    label: "Tarefas",
    segment: "tarefas",
    soon: false,
  },
  { icon: Boxes, label: "Estoque", segment: "estoque", soon: false },
]

/** Módulo para onde se vai ao escolher um board fora de um módulo (ex.: de /boards). */
export const DEFAULT_MODULE_SEGMENT = "dashboard"

/** Módulo de um endereço /board/{id}/{segment}; undefined fora de um módulo. */
export function moduleSegmentOf(pathname: string) {
  const [, area, , segment] = pathname.split("/")

  return area === "board" &&
    BOARD_MODULES.some((item) => item.segment === segment)
    ? segment
    : undefined
}
