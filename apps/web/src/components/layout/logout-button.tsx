import { Button } from "@/components/ui/button";

// POST direto para a API (mesmo origin, via /api): ela encerra a sessão e redireciona para /login.
export function LogoutButton() {
  return (
    <form method="post" action="/api/auth/logout">
      <Button type="submit" variant="outline" className="h-8 px-3">
        Sair
      </Button>
    </form>
  );
}
