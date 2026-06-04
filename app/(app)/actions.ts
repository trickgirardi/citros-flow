"use server";

import { redirect } from "next/navigation";

import { signOutCurrentSession } from "@/lib/supabase/queries/auth";

export async function logout() {
  await signOutCurrentSession();
  redirect("/login");
}
