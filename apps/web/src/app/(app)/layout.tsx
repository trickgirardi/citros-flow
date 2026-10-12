import { redirect } from "next/navigation"

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
      {children}
    </div>
  )
}
