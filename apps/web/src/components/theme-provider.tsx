"use client"

import * as React from "react"

type ResolvedTheme = "dark" | "light"

const THEME_STORAGE_KEY = "theme"

function ThemeProvider({ children }: React.PropsWithChildren) {
  React.useEffect(() => {
    const storedTheme = getStoredTheme()
    const nextTheme = storedTheme ?? getSystemTheme()

    applyTheme(nextTheme)
  }, [])

  const toggleTheme = React.useCallback(() => {
    const currentTheme = document.documentElement.classList.contains("dark")
      ? "dark"
      : "light"
    const nextTheme = currentTheme === "dark" ? "light" : "dark"

    applyTheme(nextTheme)
    localStorage.setItem(THEME_STORAGE_KEY, nextTheme)
  }, [])

  return (
    <>
      <ThemeHotkey onToggleTheme={toggleTheme} />
      {children}
    </>
  )
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

function ThemeHotkey({ onToggleTheme }: { onToggleTheme: () => void }) {
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

      onToggleTheme()
    }

    window.addEventListener("keydown", onKeyDown)

    return () => {
      window.removeEventListener("keydown", onKeyDown)
    }
  }, [onToggleTheme])

  return null
}

function getStoredTheme(): ResolvedTheme | null {
  const storedTheme = localStorage.getItem(THEME_STORAGE_KEY)

  return storedTheme === "dark" || storedTheme === "light" ? storedTheme : null
}

function getSystemTheme(): ResolvedTheme {
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"
}

function applyTheme(theme: ResolvedTheme) {
  document.documentElement.classList.toggle("dark", theme === "dark")
  document.documentElement.style.colorScheme = theme
}

export { ThemeProvider }
