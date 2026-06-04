import { redirect } from "next/navigation"

import { LogoutButton } from "@/components/layout/logout-button"
import { getCurrentUser } from "@/lib/supabase/queries/auth"

export default async function AppLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  const user = await getCurrentUser()

  if (!user) {
    redirect("/login")
  }

  return (
    <div className="flex h-svh flex-col overflow-hidden">
      <header className="flex shrink-0 items-center justify-between border-b px-4 py-3 sm:px-6">
        <div className="flex min-w-0 flex-col">
          <span className="text-sm font-semibold">Gnosis - Fluxo de Caixa</span>
          <span className="truncate text-xs text-muted-foreground">
            {user.email}
          </span>
        </div>
        <LogoutButton />
      </header>

      <main className="min-h-0 flex-1 overflow-hidden">{children}</main>
    </div>
  )
}
