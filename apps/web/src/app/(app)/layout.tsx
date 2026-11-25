import { cookies } from "next/headers"
import { redirect } from "next/navigation"

import { AppShell } from "@/components/layout/app-shell"
import { SessionKeepAlive } from "@/components/layout/session-keep-alive"
import { getCurrentUser } from "@/lib/auth"
import { listBoards } from "@/lib/boards"

export default async function AppLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  const user = await getCurrentUser()

  if (!user) {
    redirect("/login?error=session_expired")
  }

  const [boards, cookieStore] = await Promise.all([listBoards(), cookies()])

  return (
    <AppShell
      boards={boards}
      canManageBoards={user.canManageBoards}
      // Aberta por padrão; só fica fechada se a pessoa a fechou (cookie gravado pelo botão).
      defaultOpen={cookieStore.get("sidebar")?.value !== "closed"}
      userName={user.displayName ?? user.email}
    >
      <SessionKeepAlive />
      {children}
    </AppShell>
  )
}
