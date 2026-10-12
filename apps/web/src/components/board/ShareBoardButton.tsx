"use client";

import { useActionState, useEffect, useState } from "react";
import { Share2 } from "lucide-react";

import {
  createShareLink,
  type CreateShareLinkState,
} from "@/app/(app)/board/[boardId]/actions";
import { Button } from "@/components/ui/button";

const INITIAL_STATE: CreateShareLinkState = {
  error: null,
  success: false,
  url: null,
};

type ShareBoardButtonProps = {
  boardId: string;
  showLabel?: boolean;
};

export function ShareBoardButton({ boardId, showLabel = true }: ShareBoardButtonProps) {
  const [copied, setCopied] = useState(false);
  const [state, formAction, isPending] = useActionState(
    async (currentState: CreateShareLinkState, formData: FormData) => {
      formData.set("origin", window.location.origin);

      return createShareLink(currentState, formData);
    },
    INITIAL_STATE
  );

  useEffect(() => {
    if (!state.url) {
      return;
    }

    void navigator.clipboard.writeText(state.url).then(() => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    });
  }, [state.url]);

  return (
    <form action={formAction} className="inline-flex">
      <input type="hidden" name="boardId" value={boardId} />
      <input type="hidden" name="origin" value="" />
      <Button
        type="submit"
        variant="outline"
        size={showLabel ? "default" : "icon"}
        className={showLabel ? "h-8 px-2" : "size-8"}
        disabled={isPending}
        aria-label="Compartilhar board"
        title={state.error ?? state.url ?? "Compartilhar board"}
      >
        <Share2 data-icon="inline-start" />
        {showLabel ? (copied ? "Copiado" : isPending ? "Gerando..." : "Compartilhar") : null}
      </Button>
    </form>
  );
}
