"use client"

import type { ReactNode } from "react"

import { AppSidebar } from "@/components/layout/app-sidebar"
import type { ShellBoard } from "@/components/layout/board-switcher"
import {
  SidebarProvider,
  useSidebar,
} from "@/components/layout/sidebar-context"

type AppShellProps = {
  boards: readonly ShellBoard[]
  canManageBoards: boolean
  children: ReactNode
  /** Estado da sidebar no desktop, vindo do cookie (lido no servidor: sem piscar). */
  defaultOpen: boolean
  userName: string
}

/**
 * Moldura das telas logadas: sidebar à esquerda e, à direita, a coluna da tela (cabeçalho +
 * conteúdo). Cada tela desenha o seu `PageHeader`. Com a sidebar aberta o conteúdo se comprime.
 */
export function AppShell({
  boards,
  canManageBoards,
  children,
  defaultOpen,
  userName,
}: AppShellProps) {
  return (
    <SidebarProvider defaultOpen={defaultOpen}>
      <ShellFrame
        boards={boards}
        canManageBoards={canManageBoards}
        userName={userName}
      >
        {children}
      </ShellFrame>
    </SidebarProvider>
  )
}

function ShellFrame({
  boards,
  canManageBoards,
  children,
  userName,
}: Omit<AppShellProps, "defaultOpen">) {
  const { open } = useSidebar()

  return (
    <div
      data-sidebar={open ? "open" : "closed"}
      className="group/shell flex h-svh overflow-hidden"
    >
      <AppSidebar
        boards={boards}
        canManageBoards={canManageBoards}
        userName={userName}
      />
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">{children}</div>
    </div>
  )
}
