import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

/**
 * Corpo da tela, abaixo do `PageHeader`: ocupa a altura que sobra e não rola a página. É um
 * container (`@container`): o conteúdo escolhe o layout pela largura que a sidebar deixa.
 * Telas que rolam passam `overflow-y-auto`.
 */
export function PageContent({
  children,
  className,
}: {
  children: ReactNode
  className?: string
}) {
  return (
    <div
      className={cn(
        "@container min-h-0 flex-1 overflow-hidden p-3 sm:p-4",
        className
      )}
    >
      {children}
    </div>
  )
}
