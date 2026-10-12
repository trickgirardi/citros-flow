import { redirect } from "next/navigation"

import { SessionKeepAlive } from "@/components/layout/session-keep-alive"
import { getCurrentUser } from "@/lib/auth"

export default async function AppLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  const user = await getCurrentUser()

  if (!user) {
    redirect("/login?error=session_expired")
  }

  return (
    <div className="flex h-svh flex-col overflow-hidden">
      <SessionKeepAlive />
      {children}
    </div>
  )
}
