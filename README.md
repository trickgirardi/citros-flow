# Citros Flow

Base do projeto em Next.js 16 + Supabase.

## Fase 1 - Fundacao

Implementado neste repositório:

- Configuracao Supabase SSR (`lib/supabase/*`)
- Login/logout com Supabase Auth (`/login` + server actions)
- Protecao de rotas com `proxy.ts` (Next.js 16)
- Tipos TypeScript iniciais do banco (`types/database.ts`)
- SQL de schema + RLS (`supabase/schema.sql`)
- Variaveis de ambiente em `.env.example`

## Setup

1. Copie `.env.example` para `.env.local` e preencha as chaves do Supabase.
2. No Supabase SQL Editor, execute `supabase/schema.sql`.
3. Crie usuarios em `Authentication > Users` e vinculos em `user_roles`.
4. Rode o projeto:

```bash
pnpm dev
```

## Gerar tipos oficiais do Supabase

Quando seu projeto Supabase estiver configurado, gere/atualize `types/database.ts`:

```bash
pnpm dlx supabase gen types typescript --project-id <PROJECT_ID> --schema public > types/database.ts
```
