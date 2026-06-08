import { NextResponse, type NextRequest } from "next/server";

import { updateSession } from "@/lib/supabase/proxy";

const PUBLIC_ROUTES = new Set(["/login"]);
const PUBLIC_ROUTE_PREFIXES = ["/share/"];

function isPublicRoute(pathname: string) {
  return (
    PUBLIC_ROUTES.has(pathname) ||
    PUBLIC_ROUTE_PREFIXES.some((prefix) => pathname.startsWith(prefix))
  );
}

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const { user, response } = await updateSession(request);

  if (!user && !isPublicRoute(pathname)) {
    const loginUrl = request.nextUrl.clone();
    loginUrl.pathname = "/login";
    loginUrl.searchParams.set("next", pathname);
    return NextResponse.redirect(loginUrl);
  }

  if (user && pathname === "/login") {
    const boardUrl = request.nextUrl.clone();
    boardUrl.pathname = "/board";
    boardUrl.search = "";
    return NextResponse.redirect(boardUrl);
  }

  return response;
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)"],
};
