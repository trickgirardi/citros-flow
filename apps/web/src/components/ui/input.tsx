import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * Campo de texto da marca (escala do IGB): 44px de altura, texto de 16px, raio de 14px e contorno
 * `border-input` (≥ 3:1). Nos painéis compactos do dashboard, sobrescreva com `className`
 * (ex.: `min-h-8 h-8 px-2 text-sm`).
 */
function Input({ className, type, ...props }: React.ComponentProps<"input">) {
  return (
    <input
      type={type}
      data-slot="input"
      className={cn(
        "min-h-11 w-full min-w-0 rounded-field border border-input bg-card px-4 py-2 text-base text-foreground placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:opacity-60 aria-invalid:border-2 aria-invalid:border-destructive",
        className
      )}
      {...props}
    />
  )
}

export { Input }
