import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { Slot } from "radix-ui"

import { cn } from "@/lib/utils"

const buttonVariants = cva(
  // Pílula da marca. O foco é o global (outline de 2px em globals.css). Em telas de toque o alvo
  // sobe para 44px (pointer-coarse), mesmo nos tamanhos compactos do dashboard.
  "group/button inline-flex shrink-0 items-center justify-center rounded-pill border border-transparent bg-clip-padding text-xs/relaxed font-semibold whitespace-nowrap transition select-none disabled:pointer-events-none disabled:opacity-60 aria-invalid:border-destructive [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        // CTA: amarelo da marca com texto escuro (10,9:1)
        default:
          "bg-primary text-primary-foreground hover:brightness-95 hover:shadow-card",
        // Secundário: contorno azul que preenche no hover
        outline:
          "border-2 border-accent-ink bg-card text-accent-ink hover:border-brand hover:bg-brand hover:text-on-brand",
        secondary: "bg-brand text-on-brand hover:brightness-110",
        ghost:
          "text-foreground hover:bg-accent aria-expanded:bg-accent",
        destructive:
          "bg-danger text-on-danger hover:brightness-90 hover:shadow-card",
        link: "text-accent-ink underline-offset-4 hover:underline",
        // Sobre a faixa azul do cabeçalho
        "on-brand":
          "border-2 border-white/50 text-on-brand hover:bg-white/15",
        "on-brand-ghost": "text-on-brand hover:bg-white/15",
      },
      size: {
        // Compactos (dashboard)
        default:
          "h-7 gap-1.5 px-3 text-xs/relaxed pointer-coarse:min-h-11 [&_svg:not([class*='size-'])]:size-3.5",
        xs: "h-5 gap-1 px-2 text-[0.625rem] [&_svg:not([class*='size-'])]:size-2.5",
        sm: "h-6 gap-1 px-2.5 text-xs/relaxed pointer-coarse:min-h-11 [&_svg:not([class*='size-'])]:size-3",
        // Escala do IGB (modais, login, boards): texto 16px, alvo de 44px
        lg: "min-h-11 gap-2 px-6 py-2 text-base",
        icon: "size-7 pointer-coarse:size-11 [&_svg:not([class*='size-'])]:size-3.5",
        "icon-xs": "size-5 [&_svg:not([class*='size-'])]:size-2.5",
        "icon-sm":
          "size-6 pointer-coarse:size-11 [&_svg:not([class*='size-'])]:size-3",
        "icon-lg": "size-11 [&_svg:not([class*='size-'])]:size-5",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

function Button({
  className,
  variant = "default",
  size = "default",
  asChild = false,
  ...props
}: React.ComponentProps<"button"> &
  VariantProps<typeof buttonVariants> & {
    asChild?: boolean
  }) {
  const Comp = asChild ? Slot.Root : "button"

  return (
    <Comp
      data-slot="button"
      data-variant={variant}
      data-size={size}
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  )
}

export { Button, buttonVariants }
