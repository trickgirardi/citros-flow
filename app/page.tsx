import { redirect } from "next/navigation";

import { getCurrentUser } from "@/lib/supabase/queries/auth";

export default async function Page() {
  const user = await getCurrentUser();

  if (user) {
    redirect("/board");
  }

  redirect("/login");
}
