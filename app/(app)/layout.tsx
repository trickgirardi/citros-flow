import { redirect } from "next/navigation";

import { LogoutButton } from "@/components/layout/logout-button";
import { getCurrentUser } from "@/lib/supabase/queries/auth";

export default async function AppLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const user = await getCurrentUser();

  if (!user) {
    redirect("/login");
  }

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b px-6 py-3">
        <div className="flex flex-col">
          <span className="text-sm font-semibold">Citros Flow</span>
          <span className="text-xs text-muted-foreground">{user.email}</span>
        </div>
        <LogoutButton />
      </header>

      <main className="flex-1">{children}</main>
    </div>
  );
}
