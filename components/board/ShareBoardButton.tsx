import { Share2 } from "lucide-react";

import { Button } from "@/components/ui/button";

type ShareBoardButtonProps = {
  showLabel?: boolean;
};

export function ShareBoardButton({ showLabel = true }: ShareBoardButtonProps) {
  return (
    <Button
      type="button"
      variant="outline"
      size={showLabel ? "default" : "icon"}
      className={showLabel ? "h-8 px-2" : "size-8"}
      disabled
      aria-label="Compartilhar board"
    >
      <Share2 data-icon="inline-start" />
      {showLabel ? "Compartilhar" : null}
    </Button>
  );
}
