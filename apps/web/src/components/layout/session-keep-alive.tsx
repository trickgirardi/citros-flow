"use client"

import { useEffect } from "react"

// A API renova o cookie de sessão (Set-Cookie) quando faltam menos de 2 dias para vencer. Mas as
// chamadas feitas pelo servidor do Next nao repassam Set-Cookie para o browser. Este ping vai direto
// do browser (via /api), entao o cookie renovado chega. Tambem descobre cedo uma sessao vencida.
const MIN_INTERVAL_MS = 10 * 60 * 1000

export function SessionKeepAlive() {
  useEffect(() => {
    let lastPing = 0

    async function ping() {
      lastPing = Date.now()

      try {
        const response = await fetch("/api/me", {
          cache: "no-store",
          credentials: "same-origin",
        })

        if (response.status === 401) {
          // Sessao vencida ou usuario desativado: volta ao login em vez de deixar uma tela que nao funciona.
          const next = encodeURIComponent(
            `${window.location.pathname}${window.location.search}`
          )
          window.location.assign(`/login?error=session_expired&next=${next}`)
        }
      } catch {
        // Sem rede ou API reiniciando nao e motivo para deslogar. O proximo ping tenta de novo.
      }
    }

    function handleVisibilityChange() {
      if (
        document.visibilityState === "visible" &&
        Date.now() - lastPing > MIN_INTERVAL_MS
      ) {
        void ping()
      }
    }

    void ping()
    document.addEventListener("visibilitychange", handleVisibilityChange)

    return () =>
      document.removeEventListener("visibilitychange", handleVisibilityChange)
  }, [])

  return null
}
