"use client"

import * as React from "react"

const DESKTOP_QUERY = "(min-width: 1024px)"
const SIDEBAR_COOKIE = "sidebar"

type SidebarState = {
  /** Desktop: sidebar aberta ao lado do conteúdo. */
  open: boolean
  /** Celular: gaveta aberta por cima do conteúdo. */
  mobileOpen: boolean
  setMobileOpen: (open: boolean) => void
  /** Desktop abre/fecha a sidebar (e lembra); celular abre/fecha a gaveta. */
  toggle: () => void
}

const SidebarContext = React.createContext<SidebarState | null>(null)

// Cookie (e não localStorage): o servidor lê no próximo carregamento e já desenha a sidebar no
// estado certo, sem piscar.
function saveSidebarCookie(open: boolean) {
  document.cookie = `${SIDEBAR_COOKIE}=${open ? "open" : "closed"}; path=/; max-age=31536000; samesite=lax`
}

function SidebarProvider({
  children,
  defaultOpen,
}: React.PropsWithChildren<{ defaultOpen: boolean }>) {
  const [open, setOpen] = React.useState(defaultOpen)
  const [mobileOpen, setMobileOpen] = React.useState(false)

  const toggle = React.useCallback(() => {
    if (window.matchMedia(DESKTOP_QUERY).matches) {
      saveSidebarCookie(!open)
      setOpen(!open)
    } else {
      setMobileOpen((current) => !current)
    }
  }, [open])

  // A gaveta é só do celular: ao crescer para o desktop ela fecha (a sidebar fixa assume).
  React.useEffect(() => {
    const query = window.matchMedia(DESKTOP_QUERY)
    const onChange = (event: MediaQueryListEvent) => {
      if (event.matches) {
        setMobileOpen(false)
      }
    }

    query.addEventListener("change", onChange)

    return () => query.removeEventListener("change", onChange)
  }, [])

  const value = React.useMemo(
    () => ({ open, mobileOpen, setMobileOpen, toggle }),
    [open, mobileOpen, toggle]
  )

  return <SidebarContext value={value}>{children}</SidebarContext>
}

function useSidebar() {
  const context = React.useContext(SidebarContext)

  if (!context) {
    throw new Error("useSidebar precisa estar dentro do AppShell.")
  }

  return context
}

export { SidebarProvider, useSidebar }
