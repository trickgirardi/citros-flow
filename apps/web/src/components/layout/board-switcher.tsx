"use client"

import Link from "next/link"
import { Check, ChevronsUpDown, Landmark } from "lucide-react"

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

export type ShellBoard = { id: string; name: string }

const TRIGGER_CLASS =
  "flex min-h-11 w-full items-center gap-2 rounded-field border border-input bg-card px-3 py-2 text-left text-base leading-tight font-semibold disabled:cursor-not-allowed disabled:opacity-60"

type BoardSwitcherProps = {
  boards: readonly ShellBoard[]
  current: ShellBoard | null
  /** Endereço do board escolhido, no mesmo módulo em que a pessoa está. */
  hrefFor: (boardId: string) => string
  onNavigate?: () => void
}

/** Seletor de board do topo da sidebar. Trocar de board mantém o módulo atual. */
export function BoardSwitcher({
  boards,
  current,
  hrefFor,
  onNavigate,
}: BoardSwitcherProps) {
  if (!current) {
    return (
      <button type="button" disabled className={TRIGGER_CLASS}>
        <Landmark className="size-5 shrink-0 text-accent-ink" />
        <span className="min-w-0 flex-1 truncate">Nenhum board</span>
      </button>
    )
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className={TRIGGER_CLASS}
          aria-label={`Board atual: ${current.name}. Trocar de board`}
        >
          <Landmark className="size-5 shrink-0 text-accent-ink" />
          <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">
            {current.name}
          </span>
          <ChevronsUpDown className="size-4 shrink-0 text-muted-foreground" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuLabel>Boards</DropdownMenuLabel>
        {boards.map((board) => (
          <DropdownMenuItem key={board.id} asChild>
            <Link
              href={hrefFor(board.id)}
              onClick={onNavigate}
              aria-current={board.id === current.id ? "true" : undefined}
            >
              <span className="min-w-0 flex-1 truncate">{board.name}</span>
              {board.id === current.id ? (
                <Check className="text-accent-ink" />
              ) : null}
            </Link>
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
