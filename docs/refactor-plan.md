# Plano de refatoração: Supabase → ASP.NET Core + PostgreSQL

> Status: aprovado em 2026-10-12. Etapas 0, 1 e 2 concluídas. Demais etapas não iniciadas.

## 0. Contexto

O Citros Flow hoje é um app Next.js 16 que acessa o Supabase diretamente: auth por e-mail/senha, Postgres com RLS e chave service-role para a página pública. A meta é:

- tirar o Supabase;
- ter um backend próprio em ASP.NET Core (EF Core + Npgsql) com Postgres via Docker Compose;
- login apenas com Google, liberado só para e-mails pré-cadastrados;
- front e API sob o mesmo origin.

**Escopo desta refatoração:** dev local, com Postgres 18 no docker-compose, a API .NET e o web Next.

**Fora do escopo** (vai para o backlog, seção 9): reverse proxy (Caddy), domínio, VPS, Dockerfiles e compose de produção, backup. A arquitetura fica pronta para isso: mesmo origin e API sob `/api`.

**Decisões confirmadas**

| Tema | Decisão |
|---|---|
| Dados | Começar do zero, só com seed SQL |
| Repositório | Monorepo `apps/web` + `apps/api` + `infra/` |
| Dinheiro | String decimal no JSON |
| Testes | xUnit + Testcontainers |
| Stack | .NET 10 LTS, PostgreSQL 18, Minimal APIs por feature (sem Controllers) |
| Papel global | Qualquer papel global lê todos os boards; admin ou tesoureiro global escreve |
| Compartilhamento | Botão escondido para viewer; corrigir o link "Hoje" da página pública; revogar e listar links vão para o backlog |
| Categorias | Texto livre (paridade). Tabela própria vai para o backlog |
| Admin | O seed SQL basta e precisa estar documentado para produção. Tela de admin fica fora |
| Sessão | Cookie de 7 dias deslizantes, revalidando `is_active` a cada request |
| Auditoria | `transactions` ganha `updated_at`/`updated_by` (desvio registrado em relação ao `schema.sql`) |
| Limpeza | Remover `next-themes`, `lib/supabase/admin.ts` e `client.ts` em commit separado |
| AGENTS | `AGENTS.md` continua sendo a fonte; `CLAUDE.md` contém `@AGENTS.md`; a regra "Use supabase skill" sai |
| Google OAuth | O usuário cria os clients. O README traz o passo a passo com as URIs exatas |
| Proxy em dev | Next `rewrites` levam `/api/*` à API .NET (mesmo origin, sem CORS). O `CallbackPath` fica sob `/api` |
| Nomes no EF | `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`), com `HasColumnName`/`ToTable` só nas exceções |
| OpenAPI e docs | Documento gerado no `dotnet build` (o `gen:api` não precisa da API rodando). UI Scalar só em Development |

---

## 1. Estado atual

### 1.1 Arquitetura
- **Stack:** Next.js 16.2.6 (App Router, `proxy.ts` no lugar de `middleware.ts`), React 19.2.4, Tailwind 4, shadcn (só `button` e `dialog`). Usa pnpm, mas há dois lockfiles (`pnpm-lock.yaml` e `package-lock.json`).
- **Acesso a dados:** todo acesso ao banco passa por `lib/supabase/queries/*`. As leituras ficam em Server Components. As escritas são Server Actions em `app/(app)/board/[boardId]/actions.ts`, que chamam a query e depois `revalidatePath`. Nenhum client component usa o Supabase diretamente.
- **Regras de negócio** (todas em TypeScript):
  - somas e agrupamentos em `components/panels/dashboard-data.ts`;
  - mês e fuso em `components/board/month-scope.ts`: `America/Sao_Paulo`, parâmetro `?month=YYYY-MM`;
  - parser do CSV do Nubank em `actions.ts:96-200`;
  - imagem do fechamento gerada em canvas no cliente (`MonthlyClosingImageModal.tsx`).
- **Não existem:** testes, CI, Dockerfile, `docs/` e o `.env.example` citado no README. O spec fica em `.specs/core-spec.md`.

### 1.2 Inventário do que depende do Supabase

| Área | Onde | Observação |
|---|---|---|
| Dependências | `package.json`: `@supabase/ssr`, `@supabase/supabase-js` | |
| Clients | `lib/supabase/server.ts` (cookies SSR), `proxy.ts` (refresh de sessão), `service.ts` (service role, só no share) | `client.ts` e `admin.ts` **não são usados** |
| Env | `lib/supabase/env.ts`: `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` (fallback `..._ANON_KEY`), `SUPABASE_SERVICE_ROLE_KEY` | Único lugar que lê `process.env` |
| Auth | `lib/supabase/queries/auth.ts`, `app/(auth)/login`, `forgot-password`, `reset-password`, `app/auth/confirm/route.ts`, `app/(app)/actions.ts` (logout) | E-mail/senha e reset por PKCE. Sem OAuth e sem sign-up |
| Proteção de rotas | `proxy.ts` e `app/(app)/layout.tsx` | Rotas públicas: `/login`, `/forgot-password`, `/reset-password`, `/share/*`, `/auth/confirm` |
| Papéis | `boards.ts:36` (`canMutateBoardForCurrentUser`) lê `user_roles` | Só controla a UI. As actions dependem do RLS |
| Queries | `boards.ts`, `transactions.ts`, `financial-reserves.ts`, `share-links.ts` | Ver a seção 5 |
| Schema e RLS | `supabase/schema.sql`, aplicado manualmente | 6 tabelas e 16 policies. Sem functions, views, triggers, migrations ou seeds |
| Tipos | `types/database.ts`, escrito à mão | Também importado por `components/panels/dashboard-data.ts` |
| Storage, realtime, edge functions | **nenhum** | O CSV é lido em memória e o PNG é gerado no browser |
| Tooling | `.mcp.json` (Supabase MCP), linha "supabase skill" no `AGENTS.md`, README | |

### 1.3 Schema atual (resumo)

| Tabela | Colunas |
|---|---|
| `accounts` | `id`, `name`, `created_at` |
| `boards` | `id`, `account_id` → accounts, `name`, `created_at` |
| `transactions` | `id`, `board_id`, `type` (check entrada\|saida), `amount numeric(12,2) > 0`, `description`, `category` (texto livre), `date`, `created_by` → auth.users, `created_at` |
| `user_roles` | `id`, `user_id` → auth.users, `board_id` (NULL = global), `role` (check admin\|tesoureiro\|viewer), `unique(user_id, board_id)` |
| `board_share_links` | `id`, `board_id`, `token_hash` (unique), `created_by`, `created_at`, `revoked_at`, `expires_at` |
| `financial_reserves` | `id`, `board_id`, `name`, `amount numeric(12,2) > 0`, `created_by`, `created_at` |

Regras de acesso do RLS hoje:
- **Leitura:** membro do board, ou admin global.
- **Escrita** em transactions, reserves e share_links: admin ou tesoureiro no board, ou com papel global. Inserts também exigem `created_by` igual ao usuário.
- **Inconsistência:** um tesoureiro global escreve, mas não lê. A Q1 resolveu isso: a leitura passa a valer para qualquer papel global.

### 1.4 Problemas que o refactor resolve
- **Saldo anterior errado acima de 1000 linhas.** `listTransactionsBeforeDate` traz todas as linhas e soma no JS, mas o PostgREST corta em 1000 linhas, então o saldo fica errado sem aviso. Na API vira `SUM` no SQL.
- **Somas em float.** Hoje os valores são somados com float em JS.
- **Actions sem checagem de permissão.** A atualização de categoria em massa responde "sucesso" com 0 linhas afetadas quando o RLS bloqueia.
- **Botão de compartilhar visível para viewer.** O clique falha.
- **Link "Hoje" errado na página pública.** Fica em `app/share/[token]/page.tsx:78`, que passa o mês sendo visto em vez do mês atual.
- **Código morto:** `lib/supabase/client.ts`, `admin.ts`, `next-themes` e `package-lock.json`.

---

## 2. Conflitos com as decisões (ciência registrada)

1. **"Manter todas as funcionalidades" × "apenas login Google".** Login por senha, `forgot-password`, `reset-password` e `/auth/confirm` **deixam de existir**.
2. **Princípio do README "RLS no banco garante…".** A autorização passa para a API (`BoardAccess`), que vira a única porta de acesso ao Postgres. README e spec serão atualizados.
3. **`Features/Categorias/`.** Não vai existir: categoria continua texto livre, e as sugestões ficam em `Features/Transacoes`.
4. **Não há UI para criar boards, contas ou papéis.** O cadastro é feito pelo seed SQL (seção 6).
5. **`apps/web/src/`.** O app hoje não usa `src/`. A mudança é mecânica e fica isolada na Etapa 2.
6. **Desvio de schema:** `transactions.updated_at` e `updated_by` (Q7).

---

## 3. Arquitetura alvo (escopo atual: dev local)

```
Browser ──> Next (localhost:3000)
              ├── /api/*  ──rewrite (next.config)──> API .NET (localhost:5080)
              └── páginas (Server Components / Server Actions)
                     └──HTTP direto + Cookie repassado──> API .NET (API_INTERNAL_URL)
API ──EF Core/Npgsql──> Postgres 18 (docker-compose.dev.yml, localhost:5432)
```

Futuro, fora deste escopo: o Caddy assume o papel do rewrite (`/api/*` → api, o resto → web) sem mudar a API nem o front.

### 3.1 Front
- Mantém Server Components e Server Actions, para mexer o mínimo na UI.
- `lib/supabase/queries/*` é substituído por chamadas à API em `API_INTERNAL_URL`, repassando o header `Cookie` do request.
- Nenhum token vai para o browser: o cookie é HttpOnly.

### 3.2 Fluxo de auth (Google + cookie + allowlist)
1. O botão "Entrar com Google" leva a `/login` → `/api/auth/login?returnUrl=/board`. O rewrite repassa a chamada para a API, que faz o Challenge do Google.
2. O Google redireciona para `http://localhost:3000/api/auth/signin-google`, que também chega à API pelo rewrite.
3. Em `OnTicketReceived`, a API exige `email_verified = true` e procura o e-mail normalizado (trim + lower) em `users` com `is_active = true`.
   - Se não achar, falha sem criar sessão e redireciona para `/login?error=not_allowed` (ou `unverified`).
   - Se achar, troca a identidade pelos claims internos (`sub = users.id`, email, nome), atualiza `last_login_at`, emite o cookie `citros_session` e redireciona para o `returnUrl`.
4. Cookie: HttpOnly, SameSite=Lax, `Secure` conforme o request (Always em produção futura), 7 dias com expiração deslizante.
5. `OnValidatePrincipal` consulta `users.is_active` em todo request. Usuário inativo tem o principal rejeitado e o cookie apagado.
6. Para rotas `/api/*`, falta de autenticação responde 401 e falta de permissão responde 403, em JSON e sem redirect.
7. `proxy.ts` só verifica se o cookie existe, porque não consegue decifrá-lo (Data Protection). Quem valida de fato é `app/(app)/layout.tsx`, chamando `GET /api/me`. Um 401 manda para `/login`.

### 3.3 Validação do rewrite do Next com o OAuth (pedido da Q11)

O código foi conferido em `next/dist/server/lib/router-utils/proxy-request.js` do Next 16.3.4 (o projeto usa 16.2.6; reconfirmar após `pnpm install`). O rewrite para uma URL externa usa `httpxy` com `changeOrigin: true` e só acrescenta `x-forwarded-host`.

**Problema.** A API recebe `Host: localhost:5080`, recebe `X-Forwarded-Host: localhost:3000` e **não** recebe `X-Forwarded-Proto`. Por padrão, o handler do Google monta o `redirect_uri` a partir de `Request.Scheme` e `Request.Host`. Sem ajuste, o resultado seria `http://localhost:5080/api/auth/signin-google`, que está errado.

**Solução: configuração `App:PublicOrigin`.**
- Em dev, `App:PublicOrigin` vale `http://localhost:3000`.
- Um middleware no início do pipeline, antes de `UseAuthentication`, sobrescreve `Request.Scheme` e `Request.Host` com esse valor.
- Assim o `redirect_uri` sai como `http://localhost:3000/api/auth/signin-google`.
- Pelo mesmo motivo, os redirects absolutos (`returnUrl`, erro) apontam para o Next.
- A solução é determinística e não confia em headers. Quando o Caddy entrar, basta apontar `PublicOrigin` para `https://<domínio>`.
- A alternativa seria `UseForwardedHeaders` com `XForwardedHost`. Ela foi descartada porque não resolve o scheme e depende de proxies conhecidos.

**Cookies através do rewrite.**
- O `httpxy` repassa `Set-Cookie` sem `Domain`, então os cookies ficam host-only em `localhost:3000`. Vale tanto para o cookie de correlação e nonce do OAuth quanto para o `citros_session`.
- O retorno do Google é uma navegação GET de topo, então o cookie de correlação com SameSite=Lax chega à API.
- Em dev, configurar `CorrelationCookie.SameSite = Lax` e `SecurePolicy = SameAsRequest`. O padrão do ASP.NET é SameSite=None com Secure, que depende da exceção de "secure context" para localhost e é frágil.

**Ordem `proxy.ts` × rewrites.** O `proxy.ts` roda **antes** dos rewrites do `next.config`. O matcher precisa **excluir `/api`**. Sem isso, `/api/auth/login` sem cookie seria redirecionado para `/login` e o login nunca começaria.

**Configuração do rewrite.**
- Fica em `next.config.ts`, em `rewrites()`: `/api/:path*` → `${API_INTERNAL_URL}/api/:path*`.
- Os rewrites são gravados no build (`routes-manifest`). Por isso, `API_INTERNAL_URL` precisa estar definido no `next build`.
- No futuro com Caddy, `/api` nem chega ao Next e o rewrite fica inofensivo.
- Timeout do proxy do Next: 30s. É suficiente. O upload de CSV não passa pelo rewrite: vai por Server Action direto para a API.

**Teste de aceite (Etapa 4).**
- Pelo `localhost:3000`, o login completo funciona.
- No DevTools, o `redirect_uri` enviado ao Google é exatamente `http://localhost:3000/api/auth/signin-google`.
- `citros_session` aparece em `localhost:3000`.
- `GET /api/me` via rewrite responde 200.

### 3.4 Dinheiro
- Banco: `numeric(12,2)`. C#: `decimal` com `HasPrecision(12,2)`. JSON: string `"1234.56"`.
  - A serialização usa um `JsonConverter<decimal>` invariante.
  - Um schema transformer do OpenAPI publica esse tipo como `type: string, format: decimal`.
- Totais, grupos por categoria e saldos chegam **prontos da API**. O front só formata e usa `Number(s)` apenas para exibição.
- Os ajustes temporários do modal de imagem são somados em centavos inteiros (`src/lib/money.ts`).
- Entrada nos formulários: o front normaliza `"1.234,56"` para `"1234.56"`. A API faz o parse para `decimal`, exige valor maior que 0 e arredonda para 2 casas com `AwayFromZero`, igual ao `Math.round` atual.

### 3.5 Contrato OpenAPI e cliente TypeScript
- **Documento:** gerado por `Microsoft.AspNetCore.OpenApi` (nativo do .NET 10). Em runtime fica em `/api/openapi/v1.json`.
- **Geração no build:**
  - `Microsoft.Extensions.ApiDescription.Server` grava o documento a cada `dotnet build`, com `OpenApiGenerateDocuments=true`, `OpenApiDocumentsDirectory=../../openapi` e `--file-name openapi`.
  - O resultado é `apps/api/openapi/openapi.json`, que é **commitado**.
  - Assim, `pnpm gen:api` funciona sem subir a API e até sem o SDK .NET instalado.
- **Cuidado com o startup no build:**
  - Para produzir o documento, o gerador executa o `Program.cs` (processo `GetDocument.Insider`).
  - Nesse modo, o startup não pode aplicar migrations, conectar no banco nem exigir os secrets do Google.
  - Os pontos com efeito colateral serão protegidos por uma checagem do assembly de entrada (`GetDocument.Insider`).
  - Teste: `dotnet build` numa máquina sem Postgres e sem secrets gera o arquivo.
- **Cliente TypeScript:**
  - `openapi-typescript` lê `apps/api/openapi/openapi.json` e gera `apps/web/src/lib/api/schema.d.ts`, também commitado e não editável à mão.
  - As chamadas usam `openapi-fetch`.
- **UI de documentação:** `Scalar.AspNetCore` (2.x), mapeada **apenas em Development**, em `/api/docs`.

### 3.6 Backend
- Minimal APIs organizadas por feature, com um `MapGroup` por pasta.
- Erros em ProblemDetails.
- Autorização centralizada em `BoardAccess`:
  - `CanView` = papel no board **ou qualquer papel global**;
  - `CanMutate` = admin ou tesoureiro no board, ou admin ou tesoureiro global.
- Board invisível retorna 404, equivalente ao `notFound()` atual. Viewer tentando escrever recebe 403.
- **CSRF:** cookie SameSite=Lax mais rejeição de métodos não-GET cujo header `Origin` esteja presente e seja diferente de `App:PublicOrigin`. As chamadas server-side do Next não mandam `Origin` e passam.
- **Data Protection:** diretório de chaves configurável (`DataProtection:KeysPath`). Em dev usa o padrão do perfil do usuário. O volume persistente fica para o deploy futuro.

### 3.7 Estrutura de pastas

```
├── apps/
│   ├── web/                              # Next.js (pnpm, lockfile próprio)
│   │   ├── src/
│   │   │   ├── app/                      # rotas (sem forgot/reset/confirm)
│   │   │   ├── components/
│   │   │   ├── lib/
│   │   │   │   ├── api/schema.d.ts       # GERADO (openapi-typescript) — não editar
│   │   │   │   ├── api-client.ts         # openapi-fetch + repasse de Cookie (server-only)
│   │   │   │   └── money.ts              # formatação BRL / centavos
│   │   │   └── proxy.ts                  # matcher exclui /api
│   │   ├── next.config.ts                # rewrites /api/:path* → API_INTERNAL_URL
│   │   └── package.json                  # script gen:api
│   └── api/
│       ├── src/Financas.Api/
│       │   ├── Features/
│       │   │   ├── Auth/                 # login, callback, logout, me, allowlist, PublicOrigin
│       │   │   ├── Boards/               # lista, detalhe, BoardAccess
│       │   │   ├── Transacoes/           # mês, sugestões, CRUD, bulk categoria, import Nubank
│       │   │   ├── Reservas/
│       │   │   └── Compartilhamento/     # criar link, leitura pública por token
│       │   ├── Data/                     # AppDbContext, Entities, Configurations, Migrations/
│       │   ├── Program.cs
│       │   └── appsettings.Development.json
│       ├── openapi/openapi.json          # GERADO no dotnet build (commitado) — fonte do gen:api
│       ├── tests/Financas.Api.Tests/     # xUnit + Testcontainers.PostgreSql + WebApplicationFactory
│       └── Financas.sln
├── infra/
│   └── postgres/seed.example.sql         # account, boards, users, user_roles iniciais
├── docker-compose.dev.yml                # postgres:18
├── .env.example
├── .specs/core-spec.md
├── docs/refactor-plan.md
├── AGENTS.md                             # fonte das regras
├── CLAUDE.md                             # contém só "@AGENTS.md"
└── README.md
```

Ficam para o backlog de deploy: `docker-compose.yml` de produção, `infra/Caddyfile` e os Dockerfiles.

### 3.8 Schema alvo (EF Core, snake_case igual ao atual)

**Convenção de nomes**
- Pacote `EFCore.NamingConventions`, com `UseSnakeCaseNamingConvention()` no `AppDbContext`.
- **Compatibilidade conferida no NuGet:** a versão 10.0.1 tem target `net10.0` e depende de `Microsoft.EntityFrameworkCore` e `.Relational` `[10.0.1, 11.0.0)`. Para referência, o Npgsql EF Core está em 10.0.3.
- `ToTable` e `HasColumnName` só entram nas exceções em que o nome do `schema.sql` não sai da convenção.
- Com os `DbSet` no plural (`Accounts`, `Boards`, `Transactions`, `UserRoles`, `BoardShareLinks`, `FinancialReserves`, `Users`), todos os nomes atuais saem da convenção. Nenhuma exceção é prevista.
- A migration `Initial` será revisada contra o `schema.sql` para confirmar.

**Tabelas**
- **`users` (nova):**
  - colunas `id uuid`, `email text not null`, `display_name text null`, `is_active bool not null default true`, `created_at`, `last_login_at null`;
  - índice único em `lower(email)`;
  - o e-mail é gravado já normalizado.
- **`created_by`, `user_roles.user_id` e `transactions.updated_by`** apontam para `users(id)`.
- **`user_roles`:** `unique(user_id, board_id) NULLS NOT DISTINCT`. Corrige a duplicata de papel global.
- **`transactions`:**
  - **novas** colunas `updated_at timestamptz null` e `updated_by uuid null`, preenchidas pela API em PUT, PATCH de descrição e PATCH de categoria. É um desvio registrado em relação ao `schema.sql` e alinhado ao spec;
  - novo índice `(board_id, date)`.
- **Sem mudança:** `accounts`, `boards`, `board_share_links` e `financial_reserves`.
- Os checks `amount > 0`, `type` e `role` viram `HasCheckConstraint`.

---

## 4. Etapas

Cada etapa termina com o projeto funcionando e pode ser commitada isoladamente. Até a Etapa 9, o front continua usando o Supabase, e a API cresce em paralelo sem ser usada.

### Etapa 0: Spec e documentação
- **Escopo:**
  - gravar `docs/refactor-plan.md`;
  - atualizar `.specs/core-spec.md` com a arquitetura alvo, a auth, a regra de autorização, o contrato de dinheiro, o desvio de `updated_*` e a remoção do reset de senha.
- **Arquivos:** `docs/refactor-plan.md`, `.specs/core-spec.md`.
- **Concluído quando:** o spec descreve o estado alvo.
- **Verificação:** revisão humana.

### Etapa 1: Limpeza de código morto (commit separado, Q8)
- **Escopo:**
  - remover a dependência `next-themes`;
  - remover `lib/supabase/admin.ts` e `lib/supabase/client.ts`;
  - remover `package-lock.json`.
- **Concluído quando:** `pnpm install && pnpm typecheck && pnpm lint && pnpm build` passam.
- **Verificação:** os comandos acima e um smoke rápido (login e board).

### Etapa 2: Reestruturar o repositório (sem mudar comportamento)
- **Escopo:**
  - `git mv` do Next para `apps/web/src/...`, de `app/`, `components/`, `lib/`, `hooks/`, `types/` e `proxy.ts`;
  - mover os configs para `apps/web/`: `package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`, `tsconfig.json`, `eslint.config.mjs`, `postcss.config.mjs`, `components.json`, `next.config.ts`, `.prettierrc`;
  - ajustar o alias `@/*` → `./src/*` e os aliases do `components.json`;
  - criar o `CLAUDE.md` com `@AGENTS.md`.
- **Concluído quando:** em `apps/web`, typecheck, lint e build passam, e o app funciona igual, ainda com o Supabase.
- **Verificação:** os comandos acima e um smoke manual (login, criar e excluir transação, abrir o share).

### Etapa 3: Infra de desenvolvimento e esqueleto da API
- **Escopo:**
  - `docker-compose.dev.yml` com `postgres:18`, volume nomeado montado em **`/var/lib/postgresql`** (o caminho mudou no 18) e healthcheck;
  - `.env.example` (seção 7);
  - `Financas.sln`, `Financas.Api` e `Financas.Api.Tests`;
  - `GET /api/health`, que checa o banco;
  - `AppDbContext`, entidades e configurações das 7 tabelas da seção 3.8, mais a migration `Initial`;
  - `UseSnakeCaseNamingConvention()`;
  - `JsonConverter<decimal>` como string e o transformer do OpenAPI;
  - OpenAPI em `/api/openapi/v1.json`, gerado também no build em `apps/api/openapi/openapi.json` (seção 3.5);
  - Scalar em `/api/docs`, só em Development;
  - aplicação das migrations no startup, controlada por config e ligada em dev;
  - teste de fumaça com Testcontainers: a migration aplica, o health responde 200 e o decimal serializa como string.
- **Arquivos:** `docker-compose.dev.yml`, `.env.example`, `apps/api/**`.
- **Concluído quando:**
  - `docker compose -f docker-compose.dev.yml up -d`, `dotnet build`, `dotnet test` e `dotnet run` passam;
  - `curl localhost:5080/api/health` responde 200;
  - o schema bate com a seção 3.8 (nomes snake_case iguais ao `schema.sql`);
  - `dotnet build` **sem Postgres e sem secrets** gera `apps/api/openapi/openapi.json`.
- **Verificação:** os comandos acima, `\d+` no `psql` e um build com o Postgres parado.

### Etapa 4: Autenticação Google e allowlist
- **Escopo:**
  - tudo da seção 3.2 e 3.3: middleware de `PublicOrigin`, cookie mais Google com `CallbackPath = /api/auth/signin-google`, mapeamento do claim `email_verified` via `ClaimActions` (confirmando o nome do campo que o userinfo devolve), `OnTicketReceived` com a allowlist, `OnValidatePrincipal` checando `is_active` e o ajuste do correlation cookie em dev;
  - endpoints `GET /api/auth/login?returnUrl=` (só aceita caminho relativo que comece com `/` e não com `//`), `GET /api/auth/signin-google` (callback do handler), `POST /api/auth/logout` e `GET /api/me`;
  - filtro de `Origin` para métodos não-GET;
  - `infra/postgres/seed.example.sql`;
  - seção do README "Google OAuth" (seção 8).
- **Mudança no web, mínima e sem impacto:** adicionar o `rewrites()` no `next.config.ts` e excluir `/api` do matcher do `proxy.ts`. O app continua no Supabase; só passa a existir o caminho `/api`.
- **Concluído quando:**
  - pelo `localhost:3000`, o usuário cadastrado entra via `/api/auth/login` e `/api/me` responde 200;
  - um e-mail não cadastrado é negado sem cookie de sessão;
  - o logout invalida a sessão;
  - desativar o usuário no banco derruba a sessão no request seguinte.
- **Verificação:**
  - testes com Testcontainers e um esquema de auth fake no `WebApplicationFactory`:
    - a regra de allowlist como função pura: e-mail com caixa diferente, não verificado, inativo, inexistente;
    - `/api/me` sem cookie responde 401;
    - usuário inativo responde 401;
    - `returnUrl` malicioso é rejeitado;
  - teste manual com as credenciais OAuth de dev, seguindo o teste de aceite da 3.3.

### Etapa 5: Endpoints de leitura e autorização
- **Escopo:**
  - `BoardAccess` (seção 3.6);
  - os GETs da seção 5;
  - o resumo do mês com saldo anterior (`SUM` com sinal no SQL), totais, grupos por categoria (ordenação pt-BR), saldo final e total de reservas.
- **Arquivos:** `Features/Boards/*`, `Features/Transacoes/*` (leitura), `Features/Reservas/*` (leitura), testes.
- **Concluído quando:** todos os GETs seguem o contrato. Visibilidade:
  - viewer, tesoureiro e admin do board leem;
  - qualquer papel global lê todos os boards;
  - quem não é membro recebe 404.
- **Verificação:** testes de integração por papel e de cálculo, com mais de 1000 transações anteriores, centavos, mês vazio e virada de ano. Um arquivo `.http` para chamadas manuais.

### Etapa 6: Escrita, import e compartilhamento
- **Escopo:**
  - CRUD de transações (com `updated_at`/`updated_by`), edição de descrição, categoria em massa e CRUD de reservas;
  - import Nubank via multipart, com o parser portado para C# e as mesmas regras:
    - cabeçalho exato `Data,Valor,Identificador,Descrição`;
    - data em `DD/MM/YYYY`;
    - o sinal define entrada ou saída;
    - categoria `"Dados importados"`;
    - tudo ou nada, numa única transação de banco;
  - criar link de compartilhamento: 32 bytes aleatórios em base64url, guardando só o SHA-256;
  - `GET /api/share/{token}/months/{yyyy-MM}` anônimo, que respeita `revoked_at` e `expires_at`;
  - todas as escritas checam `CanMutate`;
  - validações iguais às atuais: descrição até 120, categoria até 80, `amount > 0`.
- **Arquivos:** `Features/Transacoes/*`, `Features/Reservas/*`, `Features/Compartilhamento/*`, testes.
- **Concluído quando:** todas as funcionalidades da seção 5 existem na API, viewer recebe 403 e o import rejeita o arquivo inteiro se uma linha for inválida.
- **Verificação:** testes de integração (permissões, parser CSV, link revogado ou expirado responde 404, `updated_*` preenchidos) e o `.http`.

### Etapa 7: Cliente TypeScript gerado (ainda sem uso)
- **Escopo:**
  - script `pnpm gen:api`, que roda `openapi-typescript ../api/openapi/openapi.json -o src/lib/api/schema.d.ts`. Não precisa subir a API, porque o arquivo já vem commitado e é atualizado a cada `dotnet build`;
  - `src/lib/api-client.ts`, marcado `server-only`, usando `openapi-fetch` com `API_INTERNAL_URL` e o `Cookie` de `cookies()`;
  - `src/lib/money.ts`.
- **Concluído quando:** `pnpm gen:api && pnpm typecheck` passam e o app continua no Supabase.
- **Verificação:** typecheck. Regenerar sem mudar a API produz `git diff` vazio.

### Etapa 8: Virada do front para a API
- **Escopo:**
  - **Auth:**
    - `/login` vira o botão "Entrar com Google" e mostra os erros `not_allowed` e `unverified`;
    - `proxy.ts` checa o cookie `citros_session`;
    - `(app)/layout.tsx` usa `/api/me`;
    - o logout vira um form POST para `/api/auth/logout`;
    - remover `forgot-password`, `reset-password`, `auth/confirm` e os forms correspondentes.
  - **Dados:**
    - as páginas `board/page.tsx`, `board/[boardId]/layout.tsx`, `board/[boardId]/page.tsx` e `share/[token]/page.tsx` passam a consumir a API;
    - as Server Actions chamam a API, mapeiam ProblemDetails para `{error}` e mantêm `revalidatePath`;
    - o parser CSV sai do front, e a action só repassa o arquivo.
  - **UI:**
    - `dashboard-data.ts` mantém só formatação e tipos, e os grupos e totais chegam prontos;
    - o modal de imagem passa a somar em centavos;
    - `ShareBoardButton` só aparece com `canMutate`;
    - corrigir o link "Hoje" da página pública para o mês atual.
- **Arquivos (representativos):** `src/proxy.ts`, `src/app/(auth)/login/*`, `src/app/(app)/**`, `src/app/share/[token]/page.tsx`, `src/components/panels/*`, `src/components/board/MonthlyClosingImageModal.tsx`, `ShareBoardButton.tsx`, `src/components/layout/logout-button.tsx`.
- **Concluído quando:** todas as funcionalidades funcionam via `localhost:3000` e nada importa `lib/supabase`.
- **Verificação:**
  - `pnpm typecheck && pnpm lint && pnpm build`;
  - roteiro manual:
    - login permitido e negado;
    - trocar de mês;
    - criar, editar e excluir transação;
    - descrição inline;
    - categoria em massa;
    - import CSV;
    - CRUD de reservas;
    - gerar link e abrir em janela anônima, conferindo o "Hoje";
    - imagem do fechamento;
    - logout;
    - viewer sem ações e sem botão de compartilhar.

### Etapa 9: Remover o Supabase
- **Escopo:**
  - remover `@supabase/*` do `package.json`;
  - apagar `src/lib/supabase/`, `src/types/database.ts` e `supabase/schema.sql` (fica no histórico do git);
  - tirar a entrada do Supabase do `.mcp.json`;
  - tirar a linha "Use supabase skill" do `AGENTS.md`;
  - reescrever o README (setup dev, seed, Google OAuth);
  - atualizar o princípio "RLS no banco".
- **Concluído quando:** `grep -ri supabase apps/ infra/ *.md .mcp.json` não retorna nada relevante e o build passa.
- **Verificação:** o grep, `pnpm build` e o smoke da Etapa 8.

### Paralelismo
- As Etapas 1 e 2 vão primeiro, porque mexem em todos os paths do front.
- Depois da Etapa 3, a Etapa 4 (auth) e as Etapas 5 e 6 (endpoints, testados com auth fake) podem andar em paralelo.
- A Etapa 7 começa quando o contrato das Etapas 5 e 6 estabilizar.
- A Etapa 0 (spec) pode ser revisada em paralelo à Etapa 1.

---

## 5. Mapeamento funcionalidade → endpoint

Todos os endpoints exigem sessão, exceto os marcados como públicos. Leitura de board exige `CanView`; escrita exige `CanMutate`.

| Funcionalidade atual | Hoje | Endpoint |
|---|---|---|
| Health | — | `GET /api/health` (público) |
| Login | `login/actions.ts` (senha) | `GET /api/auth/login?returnUrl=` (público) → Google → `GET /api/auth/signin-google` |
| Logout | `app/(app)/actions.ts` | `POST /api/auth/logout` |
| Usuário atual | `getCurrentUser` | `GET /api/me` → `{id, email, displayName}` |
| Esqueci e redefinir senha | `forgot-password`, `reset-password`, `auth/confirm` | **removidos** (conflito 1) |
| Listar boards (redireciona para o primeiro) | `listBoardsForCurrentUser` | `GET /api/boards` |
| Board e permissão | `getBoardForCurrentUser` + `canMutateBoardForCurrentUser` | `GET /api/boards/{boardId}` → `{id, name, canMutate}` |
| Dashboard do mês (transações, saldo anterior, totais, grupos, total de reservas) | `listTransactionsByBoard`, `listTransactionsBeforeDate`, `dashboard-data.ts` | `GET /api/boards/{boardId}/months/{yyyy-MM}` |
| Sugestões de categoria e descrição | `listTransactionSuggestionsByBoard` | `GET /api/boards/{boardId}/suggestions` |
| Criar transação | `createTransaction` | `POST /api/boards/{boardId}/transactions` |
| Editar transação | `updateTransaction` | `PUT /api/boards/{boardId}/transactions/{id}` |
| Descrição inline | `updateTransactionDescription` | `PATCH /api/boards/{boardId}/transactions/{id}/description` |
| Categoria em massa | `updateTransactionsCategory` | `PATCH /api/boards/{boardId}/transactions/category` com `{ids[], category}` → `{updatedCount}` |
| Excluir transação | `deleteTransaction` | `DELETE /api/boards/{boardId}/transactions/{id}` |
| Import CSV Nubank | `importNubankCsv` | `POST /api/boards/{boardId}/transactions/import/nubank` (multipart) → `{importedCount}` |
| Listar reservas | `listFinancialReservesByBoard` | `GET /api/boards/{boardId}/reserves` |
| Criar, editar e excluir reserva | `create/update/deleteFinancialReserve` | `POST`, `PUT /{id}`, `DELETE /{id}` em `/api/boards/{boardId}/reserves` |
| Criar link de compartilhamento | `createShareLink` | `POST /api/boards/{boardId}/share-links` → `{token}` (o front monta a URL) |
| Página pública | `getBoardByShareToken` + queries com service role | `GET /api/share/{token}/months/{yyyy-MM}` (**público**) → `{boardName, ...resumo do mês}` |
| Navegação de mês, imagem do fechamento, tema | só no cliente | sem endpoint |

---

## 6. Dados e usuários iniciais

- **Migração de dados:** nenhuma. O banco começa do zero.
- **Schema:** criado só pelas migrations do EF Core. A `Initial` reproduz o `schema.sql` com os ajustes da seção 3.8.
- **Seed:** `infra/postgres/seed.example.sql` é copiado para `seed.sql`, que fica fora do git (`.gitignore`).
  - É SQL idempotente (`INSERT … ON CONFLICT DO NOTHING`) e cria `accounts`, `boards`, `users` (e-mail em minúsculas) e `user_roles` (`board_id` NULL = papel global).
  - Roda depois das migrations.
  - Dev: `docker compose -f docker-compose.dev.yml exec -T postgres psql -U $POSTGRES_USER -d $POSTGRES_DB < infra/postgres/seed.sql`.
  - Produção (documentado no README, independente do formato de deploy):
    1. subir a API uma vez para aplicar as migrations (ou rodar `dotnet ef database update` com a connection string de produção);
    2. rodar `psql "<connection string>" -v ON_ERROR_STOP=1 -f seed.sql`;
    3. conferir com `select email, role, board_id from users join user_roles …`.
  - Para incluir ou desativar usuários depois: `INSERT` pontual, ou `UPDATE users SET is_active = false`. Desativar derruba a sessão no request seguinte.
- **Testes:** cada teste cria os próprios dados no container (Testcontainers).

---

## 7. Variáveis de ambiente (`.env.example`)

| Variável | Uso | Exemplo dev |
|---|---|---|
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | compose | `citros` / `citros` / `citros` |
| `ConnectionStrings__Default` | API | `Host=localhost;Port=5432;Database=citros;Username=citros;Password=citros` |
| `Google__ClientId`, `Google__ClientSecret` | API (dev: `dotnet user-secrets`) | — |
| `App__PublicOrigin` | API: `redirect_uri`, redirects e checagem de `Origin` | `http://localhost:3000` |
| `Database__MigrateOnStartup` | API | `true` |
| `DataProtection__KeysPath` | API (opcional em dev) | vazio |
| `API_INTERNAL_URL` | web: rewrites e fetch server-side | `http://localhost:5080` |

---

## 8. README: Google OAuth (conteúdo que será escrito)

1. Google Cloud Console → APIs & Services → OAuth consent screen. Tipo **Internal** se for Google Workspace; senão **External**, publicado ou com os usuários de teste. Escopos: `openid`, `email`, `profile`.
2. Credentials → Create credentials → OAuth client ID → **Web application**.
3. **Authorized redirect URIs**, com o texto exato:
   - dev: `http://localhost:3000/api/auth/signin-google`;
   - produção (futuro): `https://<seu-domínio>/api/auth/signin-google`.

   Use clients separados para dev e produção.
4. Authorized JavaScript origins: não é necessário, porque o fluxo é server-side.
5. Dev: `dotnet user-secrets set "Google:ClientId" "…"` e `dotnet user-secrets set "Google:ClientSecret" "…"` em `apps/api/src/Financas.Api`.
6. A porta do Next em dev precisa ser 3000. Se mudar, mude também `App__PublicOrigin` e a URI no Google.

---

## 9. Riscos, atenção e backlog

### Riscos e pontos de atenção
- **`redirect_uri` atrás do rewrite.** Resolvido com `App:PublicOrigin` (seção 3.3). Se ele for configurado errado, o Google recusa com `redirect_uri_mismatch`. O teste de aceite da Etapa 4 cobre isso.
- **Matcher do `proxy.ts`.** Precisa excluir `/api`, senão o login entra em loop. O critério da Etapa 4 cobre isso.
- **`email_verified`.** O handler do Google não mapeia esse claim por padrão. Mapear errado bloqueia todo mundo ou, pior, ignora a checagem. Cobrir com teste da função de allowlist e conferir manualmente o claim recebido.
- **Repasse de cookie server-side.** Toda chamada do Next à API precisa mandar o `Cookie`, senão volta 401. A regra fica centralizada em `api-client.ts`.
- **`proxy.ts` só checa se o cookie existe.** Um cookie expirado passa pelo proxy, mas o layout pega com `/api/me`.
- **Decimal como string.** A vírgula do pt-BR precisa ser normalizada nos formulários, e a aritmética no front deve ser sempre em centavos.
- **Ordenação pt-BR.** Usar a ICU collation do Postgres (`pt-BR-x-icu`) ou ordenar em C# com `StringComparer.Create(new CultureInfo("pt-BR"), …)`. Não habilitar `InvariantGlobalization`.
- **Fuso.** O front define o mês (`America/Sao_Paulo`) e envia `yyyy-MM` explícito. A API não depende do fuso do servidor.
- **Postgres 18.**
  - A imagem oficial mudou o ponto de montagem do volume para `/var/lib/postgresql`. Montar em `/data` como antes falha ou não persiste.
  - Confirmar que o Npgsql EF Core 10 suporta o PG 18.
- **Next 16 sem `node_modules`.** Antes das Etapas 2, 4 e 8, ler os guias em `node_modules/next/dist/docs/` (proxy, rewrites), como pede o `AGENTS.md`, e reconfirmar o comportamento do `proxy-request` na 16.2.6.
- **Revalidar `is_active` a cada request.** É uma query por request autenticado. É aceitável no volume atual; a mitigação futura seria um cache curto.
- **Etapa 8 é a maior.** Auth e dados precisam virar juntos, porque a sessão do Supabase e a da API não convivem. As Etapas 3 a 7 deixam tudo pronto e testado antes da virada.
- **Testcontainers** exige Docker disponível para rodar os testes.
- **OpenAPI desatualizado.** Se alguém editar endpoints sem rodar `dotnet build`, o `openapi.json` commitado fica defasado. Como o build sempre regenera, basta revisar o diff no PR. No futuro, o CI pode falhar quando o arquivo divergir.

### Dependências novas (aprovação explícita antes de instalar)

| Onde | Dependência | Observação |
|---|---|---|
| API | `Npgsql.EntityFrameworkCore.PostgreSQL` | |
| API | `Microsoft.EntityFrameworkCore.Design` | |
| API | `Microsoft.AspNetCore.Authentication.Google` | |
| API | `Microsoft.AspNetCore.OpenApi` | |
| API | `Microsoft.Extensions.ApiDescription.Server` | Gera o `openapi.json` no build |
| API | `EFCore.NamingConventions` 10.x | Compatível com EF Core 10 (confirmado) |
| API | `Scalar.AspNetCore` 2.x | UI da documentação só em Development |
| Testes | `xunit`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql` | |
| Web | `openapi-typescript` (dev), `openapi-fetch` | |
| Removidas | `@supabase/ssr`, `@supabase/supabase-js`, `next-themes` | |

### Backlog (fora desta refatoração)
- **Deploy:** Dockerfiles (web com `output: "standalone"`, api multi-stage), `docker-compose.yml` de produção, `infra/Caddyfile` (`/api/*` → api, o resto → web, HTTPS), domínio e VPS, volume de chaves do Data Protection, `Secure=Always`, backup e restore do Postgres.
- Revogar e listar links de compartilhamento.
- Tabela de categorias.
- Tela ou endpoints de administração (usuários, boards, papéis).
- CI rodando `dotnet test` e o lint/typecheck do web.

---

## 10. Perguntas em aberto

Nenhuma. Todas as perguntas foram respondidas e incorporadas:

- Q1 a Q11 estão na tabela "Decisões confirmadas" (seção 0).
- Snake_case: `EFCore.NamingConventions`.
- Scalar: só em Development.
- OpenAPI: gerado no build.

As dependências da seção 9 ainda pedem aprovação explícita no momento da instalação, conforme o `AGENTS.md`.
