"use client"

import Link from "next/link"
import { useParams, usePathname } from "next/navigation"
import { LogOut, Settings2 } from "lucide-react"
import { Dialog as DialogPrimitive } from "radix-ui"

import {
  BoardSwitcher,
  type ShellBoard,
} from "@/components/layout/board-switcher"
import { LogoutButton } from "@/components/layout/logout-button"
import {
  BOARD_MODULES,
  DEFAULT_MODULE_SEGMENT,
  moduleSegmentOf,
} from "@/components/layout/nav-items"
import { useSidebar } from "@/components/layout/sidebar-context"
import { ThemeToggle } from "@/components/theme-provider"
import { LogoPlate } from "@/components/ui/logo"
import { initialsOf } from "@/lib/initials"
import { cn } from "@/lib/utils"

type AppSidebarProps = {
  boards: readonly ShellBoard[]
  canManageBoards: boolean
  userName: string
}

/**
 * Sidebar à esquerda (modelo do IGB). No desktop fica ao lado do conteúdo, que se comprime; no
 * celular é uma gaveta por cima. O mesmo conteúdo vale para os dois.
 */
export function AppSidebar(props: AppSidebarProps) {
  const { mobileOpen, setMobileOpen } = useSidebar()

  return (
    <>
      <aside
        id="app-sidebar"
        className="hidden w-sidebar shrink-0 flex-col border-r bg-popover lg:group-data-[sidebar=open]/shell:flex"
      >
        <SidebarContent {...props} />
      </aside>

      <DialogPrimitive.Root open={mobileOpen} onOpenChange={setMobileOpen}>
        <DialogPrimitive.Portal>
          <DialogPrimitive.Overlay className="fixed inset-0 z-40 bg-scrim/60 lg:hidden" />
          <DialogPrimitive.Content
            aria-describedby={undefined}
            className="fixed inset-y-0 left-0 z-50 flex w-[min(20rem,88vw)] flex-col bg-popover text-popover-foreground shadow-card lg:hidden"
          >
            <DialogPrimitive.Title className="sr-only">
              Menu
            </DialogPrimitive.Title>
            <SidebarContent
              {...props}
              onNavigate={() => setMobileOpen(false)}
            />
          </DialogPrimitive.Content>
        </DialogPrimitive.Portal>
      </DialogPrimitive.Root>
    </>
  )
}

function SidebarContent({
  boards,
  canManageBoards,
  onNavigate,
  userName,
}: AppSidebarProps & { onNavigate?: () => void }) {
  const pathname = usePathname()
  const { boardId } = useParams<{ boardId?: string }>()
  // Fora de um board (ex.: /boards) vale o primeiro da lista.
  const current =
    boards.find((board) => board.id === boardId) ?? boards[0] ?? null
  const activeSegment = boardId ? moduleSegmentOf(pathname) : undefined
  const hrefFor = (id: string) =>
    `/board/${id}/${activeSegment ?? DEFAULT_MODULE_SEGMENT}`

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto overscroll-contain px-2 py-3">
        <LogoPlate />

        <BoardSwitcher
          boards={boards}
          current={current}
          hrefFor={hrefFor}
          onNavigate={onNavigate}
        />

        <nav aria-label="Menu principal">
          <ul className="flex flex-col gap-1">
            {BOARD_MODULES.map((item) => (
              <li key={item.segment}>
                <NavLink
                  active={
                    current?.id === boardId && activeSegment === item.segment
                  }
                  disabled={!current}
                  href={current ? `/board/${current.id}/${item.segment}` : "#"}
                  icon={<item.icon className="size-5 shrink-0" />}
                  label={item.label}
                  onNavigate={onNavigate}
                  soon={item.soon}
                />
              </li>
            ))}
            {canManageBoards ? (
              <li className="mt-1 border-t pt-2">
                <NavLink
                  active={pathname.startsWith("/boards")}
                  href="/boards"
                  icon={<Settings2 className="size-5 shrink-0" />}
                  label="Gerenciar boards"
                  onNavigate={onNavigate}
                />
              </li>
            ) : null}
          </ul>
        </nav>
      </div>

      <div className="flex shrink-0 flex-col gap-1 border-t px-2 py-3">
        <div className="mb-2 flex items-center gap-3 rounded-card border bg-card p-3">
          <span
            aria-hidden="true"
            className="flex size-9 shrink-0 items-center justify-center rounded-pill bg-brand text-sm font-bold text-on-brand"
          >
            {initialsOf(userName)}
          </span>
          <p className="min-w-0 font-bold [overflow-wrap:anywhere]">
            {userName}
          </p>
        </div>
        <ThemeToggle
          showLabel
          className="h-11 justify-start gap-3 rounded-field px-3 text-base font-semibold"
        />
        <LogoutButton
          variant="ghost"
          className="h-11 w-full justify-start gap-3 rounded-field px-3 text-base font-semibold"
        >
          <LogOut className="size-5 shrink-0 text-destructive" />
          Sair
        </LogoutButton>
      </div>
    </div>
  )
}

const NAV_ITEM_CLASS =
  "relative flex min-h-11 items-center gap-3 rounded-field px-3 py-2 text-base"

function NavLink({
  active = false,
  disabled = false,
  href,
  icon,
  label,
  onNavigate,
  soon = false,
}: {
  active?: boolean
  disabled?: boolean
  href: string
  icon: React.ReactNode
  label: string
  onNavigate?: () => void
  soon?: boolean
}) {
  const content = (
    <>
      {active ? (
        <span
          aria-hidden="true"
          className="absolute inset-y-1.5 left-0 w-1 rounded-r-field bg-accent-ink"
        />
      ) : null}
      {icon}
      <span className="flex-1">{label}</span>
      {soon ? (
        <span className="rounded-pill border bg-muted px-2 text-xs leading-5 font-semibold text-muted-foreground">
          Em breve
        </span>
      ) : null}
    </>
  )

  if (disabled) {
    return (
      <span
        aria-disabled="true"
        className={cn(
          NAV_ITEM_CLASS,
          "cursor-not-allowed font-semibold opacity-60"
        )}
      >
        {content}
      </span>
    )
  }

  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      onClick={onNavigate}
      className={cn(
        NAV_ITEM_CLASS,
        active
          ? "bg-foreground/5 font-bold text-accent-ink"
          : "font-semibold hover:bg-foreground/5"
      )}
    >
      {content}
    </Link>
  )
}
