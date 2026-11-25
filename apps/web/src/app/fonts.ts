import { Fraunces } from "next/font/google"

/**
 * Fraunces — fonte de destaque da marca do IGB (títulos de tela e palavra de destaque).
 * SIL OFL, auto-hospedada no build. O texto corrido usa a stack do Avenir (`--font-sans`).
 */
export const display = Fraunces({
  subsets: ["latin"],
  weight: ["300", "600", "700"],
  style: ["normal", "italic"],
  display: "swap",
  variable: "--font-fraunces",
})
