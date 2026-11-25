"use client"

import type { ReactNode } from "react"
import { PanelLeft } from "lucide-react"

import { useSidebar } from "@/components/layout/sidebar-context"
import { Button } from "@/components/ui/button"
import { LogoSymbol } from "@/components/ui/logo"

type PageHeaderProps = {
  /** Ações da tela (ex.: mês, compartilhar). Cabem pelo tamanho do CABEÇALHO (container query). */
  actions?: ReactNode
  /** Linha pequena acima do título (ex.: o nome do board). */
  scope?: string
  title: string
}

/**
 * Faixa azul da tela: botão da sidebar, Sol de Aquário (só com a sidebar fechada ou no celular:
 * aberta, o logo já está nela), título em Fraunces e ações. É um container (`@container/header`):
 * as ações escolhem a versão larga ou compacta pela largura da faixa, não da janela.
 */
export function PageHeader({ actions, scope, title }: PageHeaderProps) {
  const { open, toggle } = useSidebar()

  return (
    <header className="on-brand @container/header flex min-h-15 shrink-0 items-center gap-3 bg-brand px-3 py-2 text-on-brand sm:px-4">
      <Button
        type="button"
        variant="on-brand-ghost"
        size="icon"
        aria-label="Abrir ou fechar o menu lateral"
        aria-controls="app-sidebar"
        aria-expanded={open}
        onClick={toggle}
      >
        <PanelLeft />
      </Button>
      <LogoSymbol
        size={36}
        decorative
        className="lg:hidden lg:group-data-[sidebar=closed]/shell:inline-flex"
      />
      <div className="min-w-0 flex-1">
        {scope ? <p className="truncate text-xs leading-4">{scope}</p> : null}
        <h1 className="truncate text-lg leading-6">{title}</h1>
      </div>
      {actions ? (
        <div className="flex shrink-0 items-center gap-2">{actions}</div>
      ) : null}
    </header>
  )
}
