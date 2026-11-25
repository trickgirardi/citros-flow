import type { Metadata, Viewport } from "next"

import { ThemeProvider } from "@/components/theme-provider"

import { display } from "./fonts"
import "./globals.css"

export const metadata: Metadata = {
  title: "Gnosis - Fluxo de Caixa",
  description: "Fechamento de caixa para instituições sem fins lucrativos.",
  icons: {
    // Sol de Aquário oficial do IGB.
    icon: [
      { url: "/brand/sol-de-aquario-32.png", sizes: "32x32", type: "image/png" },
      { url: "/brand/sol-de-aquario-192.png", sizes: "192x192", type: "image/png" },
      { url: "/brand/sol-de-aquario-512.png", sizes: "512x512", type: "image/png" },
    ],
    apple: [{ url: "/brand/apple-touch-icon-180.png", sizes: "180x180" }],
  },
}

// Barra do navegador no azul institucional: é a cor do cabeçalho nos dois temas.
export const viewport: Viewport = {
  themeColor: "#2350a0",
}

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html
      lang="pt-BR"
      suppressHydrationWarning
      className={`${display.variable} font-sans antialiased`}
    >
      <head>
        {/* Aplica o tema escolhido no botão antes da primeira pintura (sem piscar). Sem escolha, o CSS segue o sistema. */}
        <script
          dangerouslySetInnerHTML={{
            __html: `try{var t=localStorage.getItem("theme");if(t==="dark"||t==="light")document.documentElement.classList.add(t)}catch(e){}`,
          }}
        />
      </head>
      <body>
        <ThemeProvider>{children}</ThemeProvider>
      </body>
    </html>
  )
}
