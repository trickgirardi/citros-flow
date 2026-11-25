import type { ReactNode } from "react"

import { Button } from "@/components/ui/button"

// POST direto para a API (mesmo origin, via /api): ela encerra a sessão e redireciona para /login.
export function LogoutButton({
  children = "Sair",
  className = "h-8 px-3",
  variant = "outline",
}: {
  children?: ReactNode
  className?: string
  variant?: "outline" | "ghost"
}) {
  return (
    <form method="post" action="/api/auth/logout">
      <Button type="submit" variant={variant} className={className}>
        {children}
      </Button>
    </form>
  )
}
