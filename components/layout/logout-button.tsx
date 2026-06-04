import { logout } from "@/app/(app)/actions";
import { Button } from "@/components/ui/button";

export function LogoutButton() {
  return (
    <form action={logout}>
      <Button type="submit" variant="outline" className="h-8 px-3">
        Sair
      </Button>
    </form>
  );
}
