# Citros Flow

**Citros** é um sistema financeiro minimalista para fechamentos de caixa de instituições sem fins lucrativos. Prioriza visualização rápida e entrada ágil de dados via dashboard de tela única (single-screen no desktop, sem scroll global).

## Princípios Fundamentais
- **Clareza acima de tudo** — o usuário deve saber o saldo em menos de 2 segundos ao abrir o sistema
- **Zero fricção na entrada** — inserir uma transação deve exigir o mínimo de cliques possível
- **Fechamento mensal** — toda leitura financeira operacional acontece dentro de um mês/ano selecionado
- **Dados confiáveis** — a API é a única porta de acesso ao banco e aplica a autorização por papel em todo endpoint; dinheiro é sempre `numeric`/`decimal`, nunca float
- **IA-first, review-based** — código gerado por IA, revisado e aprovado por humano antes de merge

## Arquitetura

```
Browser ──> Next.js (apps/web, :3000)
              ├── páginas e Server Actions ──HTTP + cookie──> API ASP.NET Core (apps/api, :5080)
              └── /api/*  ──rewrite──────────────────────────> API ASP.NET Core
                                                                   └── EF Core ──> PostgreSQL 18
```

- **Front** (`apps/web`): Next.js 16 (App Router), TypeScript, Tailwind 4 e shadcn/ui. Não fala com o banco: consome a API por um cliente TypeScript **gerado** do OpenAPI (`src/lib/api/schema.d.ts`, não editar à mão).
- **API** (`apps/api`): ASP.NET Core (.NET 10, Minimal APIs por feature), EF Core + Npgsql, migrations em `Data/Migrations`. Faz o login (Google), a sessão (cookie HttpOnly), a autorização por papel e todos os cálculos financeiros.
- **Banco**: PostgreSQL 18, via `docker-compose.dev.yml`.
- **Mesmo origin**: o browser só fala com o Next. Em dev o Next encaminha `/api/*` para a API (`rewrites` em `apps/web/next.config.ts`), então não há CORS nem token no front.
- **Login**: só Google, e só para e-mails já cadastrados (tabela `users`). Não há auto-cadastro.

```
├── apps/
│   ├── web/          # Next.js
│   └── api/          # ASP.NET Core (src/Financas.Api, tests/Financas.Api.Tests, openapi/openapi.json)
├── infra/postgres/   # seed.example.sql
├── docker-compose.dev.yml
├── .env.example
├── .specs/core-spec.md   # spec do produto (fonte da verdade)
└── docs/refactor-plan.md # histórico da migração do Supabase para a API .NET
```

## Pré-requisitos

- Node.js 24 e pnpm 11
- .NET SDK 10
- Docker (Postgres em dev e testes da API)

## Setup de desenvolvimento

### 1. Postgres (Docker)

```bash
cp .env.example .env
docker compose -f docker-compose.dev.yml up -d
```

O `.env` define a porta do Postgres no host (`POSTGRES_PORT=5434`), que é a que a API usa em dev (`appsettings.Development.json`). Se trocar a porta, exporte também `ConnectionStrings__Default` ao rodar a API.

### 2. Client OAuth no Google

1. Google Cloud Console → **APIs & Services → OAuth consent screen**. Escolha **Internal** (Google Workspace) ou **External** (publicado, ou com os e-mails de teste cadastrados). Escopos: `openid`, `email`, `profile`.
2. **Credentials → Create credentials → OAuth client ID → Web application**.
3. Em **Authorized redirect URIs**, use exatamente:
   - dev: `http://localhost:3000/api/auth/signin-google`
   - produção: `https://<seu-domínio>/api/auth/signin-google`

   Use clients separados para dev e produção. **Authorized JavaScript origins** não é necessário (o fluxo é server-side).
4. Guarde o client id e o secret na API (nunca no git):

```bash
cd apps/api/src/Financas.Api
dotnet user-secrets set "Google:ClientId" "<client id>"
dotnet user-secrets set "Google:ClientSecret" "<client secret>"
```

A URI de redirect usa o host e a porta **do Next** (3000), não os da API: a API monta o `redirect_uri` a partir de `App:PublicOrigin` (`http://localhost:3000` em `appsettings.Development.json`). Se mudar a porta do Next, mude `App__PublicOrigin` e a URI no Google.

Sem `Google:ClientId`/`Google:ClientSecret` a API sobe normalmente, mas `/api/auth/login` responde 503.

### 3. Usuários permitidos (seed SQL)

Não existe tela de administração: só entra quem está na tabela `users`, ativo, com e-mail verificado no Google. Contas, boards, usuários e papéis (`admin`, `tesoureiro`, `viewer`; `board_id` nulo = papel global) são cadastrados pelo seed:

```bash
cp infra/postgres/seed.example.sql infra/postgres/seed.sql   # ignorado pelo git; edite com os dados reais
```

Escreva os e-mails em **minúsculas**. O script é idempotente (pode rodar de novo para adicionar ou ajustar pessoas). Rode **depois** das migrations (a API as aplica ao subir em Development).

- Dev: `docker compose -f docker-compose.dev.yml exec -T postgres psql -U citros -d citros -v ON_ERROR_STOP=1 < infra/postgres/seed.sql`
- Produção: suba a API uma vez para aplicar as migrations (ou rode `dotnet ef database update` com a connection string de produção) e então `psql "<connection string>" -v ON_ERROR_STOP=1 -f infra/postgres/seed.sql`

Para desativar alguém (derruba a sessão no request seguinte): `update users set is_active = false where email = '...';`

### 4. Rodar

```bash
cd apps/api && dotnet run --project src/Financas.Api    # http://localhost:5080
cd apps/web && pnpm install && pnpm dev                  # http://localhost:3000
```

Abra `http://localhost:3000` e use "Entrar com Google". E-mail não cadastrado volta para `/login` com a mensagem de acesso negado. A sessão dura 7 dias e é renovada automaticamente quando faltam menos de 2 dias.

Documentação interativa da API (só em dev): `http://localhost:3000/api/docs`. Chamadas manuais: `apps/api/src/Financas.Api/Financas.Api.http`.

### Variáveis de ambiente

| Variável | Onde | Para quê | Padrão em dev |
|---|---|---|---|
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT` | `.env` (compose) | Postgres | `citros`, `citros`, `citros`, `5434` |
| `ConnectionStrings__Default` | API | Banco | `appsettings.Development.json` (porta 5434) |
| `Google__ClientId`, `Google__ClientSecret` | API (`dotnet user-secrets` em dev) | Login Google | — |
| `App__PublicOrigin` | API | Origin público (redirect do Google, checagem de `Origin`) | `http://localhost:3000` |
| `Database__MigrateOnStartup` | API | Aplicar migrations ao subir | `true` em Development |
| `DataProtection__KeysPath` | API | Pasta das chaves que protegem o cookie de sessão | perfil do usuário |
| `API_INTERNAL_URL` | web (`apps/web/.env.local`, opcional) | Onde o Next encontra a API | `http://localhost:5080` |

## Fluxo de trabalho com o contrato da API

A API publica o OpenAPI em `apps/api/openapi/openapi.json`, regenerado a cada `dotnet build`. Depois de mudar um endpoint ou DTO:

```bash
cd apps/api && dotnet build
cd ../web && pnpm gen:api     # atualiza src/lib/api/schema.d.ts
```

Comite o `openapi.json` e o `schema.d.ts` juntos. Dinheiro trafega como string decimal (`"1234.56"`); use `src/lib/money.ts` no front.

## Testes e verificação

```bash
cd apps/api && dotnet test                                  # precisa de Docker (Testcontainers sobe um Postgres 18)
cd apps/web && pnpm typecheck && pnpm lint && pnpm build
```

## Fora do escopo por enquanto

Deploy (Dockerfiles, `docker-compose.yml` de produção, reverse proxy Caddy, domínio, VPS e backup) está no backlog do spec. A arquitetura já está pronta para isso: front e API sob o mesmo domínio, com a API em `/api`.
