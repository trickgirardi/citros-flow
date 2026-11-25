import { Button } from "@/components/ui/button";

// POST direto para a API (mesmo origin, via /api): ela encerra a sessão e redireciona para /login.
// `variant="on-brand"` para usar sobre a faixa azul do cabeçalho.
export function LogoutButton({
  variant = "outline",
}: {
  variant?: "outline" | "on-brand";
}) {
  return (
    <form method="post" action="/api/auth/logout">
      <Button type="submit" variant={variant} className="h-8 px-3">
        Sair
      </Button>
    </form>
  );
}
