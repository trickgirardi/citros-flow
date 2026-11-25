"use client"

import * as React from "react"
import { Moon, Sun } from "lucide-react"

import { Button } from "@/components/ui/button"

type Theme = "dark" | "light"

const THEME_STORAGE_KEY = "theme"

// Sem escolha salva, nenhuma classe vai no <html> e o CSS segue o sistema (prefers-color-scheme).
// O botão (e a tecla D) grava a escolha, que vale até ser trocada de novo.
function ThemeProvider({ children }: React.PropsWithChildren) {
  React.useEffect(() => {
    const storedTheme = getStoredTheme()

    if (storedTheme) {
      applyTheme(storedTheme)
    }
  }, [])

  return (
    <>
      <ThemeHotkey />
      {children}
    </>
  )
}

/** Botão de alternar tema. O ícone vem do CSS (`dark:`), então não depende de estado do React. */
function ThemeToggle({
  className,
  showLabel = false,
  variant = "ghost",
}: {
  className?: string
  /** Com rótulo ("Alternar tema") para uso em menu; sem, só o ícone. */
  showLabel?: boolean
  variant?: React.ComponentProps<typeof Button>["variant"]
}) {
  return (
    <Button
      type="button"
      variant={variant}
      size={showLabel ? "default" : "icon"}
      className={className}
      onClick={toggleTheme}
      aria-label="Alternar tema claro e escuro"
      title="Alternar tema (tecla D)"
    >
      <Moon className="dark:hidden" />
      <Sun className="hidden dark:block" />
      {showLabel ? "Alternar tema" : null}
    </Button>
  )
}

function toggleTheme() {
  const nextTheme = getCurrentTheme() === "dark" ? "light" : "dark"

  applyTheme(nextTheme)
  localStorage.setItem(THEME_STORAGE_KEY, nextTheme)
}

function isTypingTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) {
    return false
  }

  return (
    target.isContentEditable ||
    target.tagName === "INPUT" ||
    target.tagName === "TEXTAREA" ||
    target.tagName === "SELECT"
  )
}

function ThemeHotkey() {
  React.useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.defaultPrevented || event.repeat) {
        return
      }

      if (event.metaKey || event.ctrlKey || event.altKey) {
        return
      }

      if (event.key.toLowerCase() !== "d") {
        return
      }

      if (isTypingTarget(event.target)) {
        return
      }

      toggleTheme()
    }

    window.addEventListener("keydown", onKeyDown)

    return () => {
      window.removeEventListener("keydown", onKeyDown)
    }
  }, [])

  return null
}

function getStoredTheme(): Theme | null {
  const storedTheme = localStorage.getItem(THEME_STORAGE_KEY)

  return storedTheme === "dark" || storedTheme === "light" ? storedTheme : null
}

/** O tema à vista: o forçado por classe, ou o do sistema. */
function getCurrentTheme(): Theme {
  const { classList } = document.documentElement

  if (classList.contains("dark")) {
    return "dark"
  }

  if (classList.contains("light")) {
    return "light"
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"
}

function applyTheme(theme: Theme) {
  document.documentElement.classList.remove("dark", "light")
  document.documentElement.classList.add(theme)
}

export { ThemeProvider, ThemeToggle }
