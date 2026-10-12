import type { NextConfig } from "next"

// Em dev o Next faz o papel do reverse proxy: /api/* vai para a API .NET, mantendo o mesmo
// origin do browser (sem CORS). O valor é gravado no build, então precisa estar definido no `next build`.
const apiInternalUrl = process.env.API_INTERNAL_URL ?? "http://localhost:5080"

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${apiInternalUrl}/api/:path*`,
      },
    ]
  },
}

export default nextConfig
