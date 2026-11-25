import Image from "next/image"

import { cn } from "@/lib/utils"

const LOGO_ALT = "Instituto Gnosis Brasil"

/** Largura mínima da assinatura (decisão do IGB, para o slogan continuar legível). */
const SIGNATURE_MIN_WIDTH = 240

/**
 * Sol de Aquário em círculo branco: é o logo sobre o azul da marca e no tema escuro
 * (guia do IGB p. 56: o símbolo vai sobre fundo branco ou azul).
 */
function LogoSymbol({
  className,
  decorative = false,
  size = 40,
}: {
  className?: string
  decorative?: boolean
  size?: number
}) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center justify-center rounded-pill bg-white",
        className
      )}
      style={{ width: size, height: size }}
    >
      <Image
        src="/brand/sol-de-aquario-128.png"
        alt={decorative ? "" : LOGO_ALT}
        width={Math.round(size * 0.8)}
        height={Math.round(size * 0.8)}
        unoptimized
      />
    </span>
  )
}

/**
 * Assinatura completa (símbolo + nome + slogan) direto sobre fundo CLARO. No tema escuro mostra só
 * o símbolo: o "BRASIL" azul-marinho da assinatura não funciona sobre fundo escuro.
 */
function LogoAdaptive({ width = SIGNATURE_MIN_WIDTH }: { width?: number }) {
  const w = Math.max(width, SIGNATURE_MIN_WIDTH)

  return (
    <>
      <Image
        src="/brand/assinatura-2026-640.png"
        alt={LOGO_ALT}
        width={w}
        height={Math.round((w * 738) / 4115)}
        unoptimized
        priority
        className="dark:hidden"
      />
      <LogoSymbol size={72} className="hidden dark:inline-flex" />
    </>
  )
}

/**
 * Assinatura completa sobre uma placa branca: o topo da sidebar, igual nos dois temas. No claro a
 * placa se confunde com a superfície branca; no escuro ela dá o fundo claro que o "BRASIL"
 * azul-marinho da assinatura pede.
 */
function LogoPlate({ width = SIGNATURE_MIN_WIDTH }: { width?: number }) {
  const w = Math.max(width, SIGNATURE_MIN_WIDTH)

  return (
    <span className="flex justify-center rounded-field bg-white p-2">
      <Image
        src="/brand/assinatura-2026-640.png"
        alt={LOGO_ALT}
        width={w}
        height={Math.round((w * 738) / 4115)}
        unoptimized
        priority
      />
    </span>
  )
}

export { LogoAdaptive, LogoPlate, LogoSymbol }
