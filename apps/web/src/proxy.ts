import { NextResponse, type NextRequest } from "next/server";

// Nome do cookie de sessão emitido pela API. O proxy só olha se ele existe: quem valida de verdade
// é o layout (app), que pergunta à API (/api/me). Cookie vencido ou de usuário desativado passa
// aqui e é pego lá.
const SESSION_COOKIE = "citros_session";

const PUBLIC_ROUTES = new Set(["/login"]);
const PUBLIC_ROUTE_PREFIXES = ["/share/"];

function isPublicRoute(pathname: string) {
  return (
    PUBLIC_ROUTES.has(pathname) ||
    PUBLIC_ROUTE_PREFIXES.some((prefix) => pathname.startsWith(prefix))
  );
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  if (isPublicRoute(pathname) || request.cookies.has(SESSION_COOKIE)) {
    return NextResponse.next();
  }

  const loginUrl = request.nextUrl.clone();
  loginUrl.pathname = "/login";
  loginUrl.search = "";
  loginUrl.searchParams.set("next", `${pathname}${search}`);
  return NextResponse.redirect(loginUrl);
}

export const config = {
  // `api/` fica de fora: o proxy roda antes dos rewrites, e /api/* (login, sessão) é da API .NET.
  matcher: ["/((?!api/|_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)"],
};
