import * as React from "react"

import { cn } from "@/lib/utils"

/** Área de texto da marca (escala do IGB): mesmo contorno e raio do `Input`. */
function Textarea({ className, ...props }: React.ComponentProps<"textarea">) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(
        "min-h-24 w-full min-w-0 rounded-field border border-input bg-card px-4 py-2 text-base text-foreground placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:opacity-60 aria-invalid:border-2 aria-invalid:border-destructive",
        className
      )}
      {...props}
    />
  )
}

export { Textarea }
