# Citros Flow

**Citros** é um sistema financeiro minimalista para fechamentos de caixa de instituições sem fins lucrativos. Prioriza visualização rápida e entrada ágil de dados via dashboard de tela única (single-screen no desktop, sem scroll global).

## Princípios Fundamentais
- **Clareza acima de tudo** — o usuário deve saber o saldo em menos de 2 segundos ao abrir o sistema
- **Zero fricção na entrada** — inserir uma transação deve exigir o mínimo de cliques possível
- **Fechamento mensal** — toda leitura financeira operacional acontece dentro de um mês/ano selecionado
- **Dados confiáveis** — RLS no banco garante que cada usuário vê apenas o que deve ver
- **IA-first, review-based** — código gerado por IA, revisado e aprovado por humano antes de merge

## Setup

1. Crie `apps/web/.env.local` com as chaves do Supabase (`NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY`, `SUPABASE_SERVICE_ROLE_KEY`).
2. No Supabase SQL Editor, execute `supabase/schema.sql`.
3. Crie usuarios em `Authentication > Users` e vinculos em `user_roles`.
4. Rode o projeto (o app Next fica em `apps/web`):

```bash
cd apps/web
pnpm install
pnpm dev
```

## Gerar tipos oficiais do Supabase

Quando seu projeto Supabase estiver configurado, gere/atualize `apps/web/src/types/database.ts` (rode a partir de `apps/web`):

```bash
pnpm dlx supabase gen types typescript --project-id <PROJECT_ID> --schema public > src/types/database.ts
```

## API .NET e login com Google (em migração)

O projeto está migrando do Supabase para uma API ASP.NET Core + PostgreSQL (veja `docs/refactor-plan.md`).
O front já consome a API (login Google, dados e escritas). O código e as dependências do Supabase ainda estão no repositório e saem na Etapa 9; as instruções de setup do Supabase acima ficam obsoletas até lá.

### 1. Postgres (Docker)

```bash
cp .env.example .env          # opcional; ajuste POSTGRES_PORT se a porta já estiver em uso
docker compose -f docker-compose.dev.yml up -d
```

### 2. Client OAuth no Google

1. Google Cloud Console → **APIs & Services → OAuth consent screen**. Escolha **Internal** (Google Workspace) ou **External** (publicado, ou com os e-mails de teste cadastrados). Escopos: `openid`, `email`, `profile`.
2. **Credentials → Create credentials → OAuth client ID → Web application**.
3. Em **Authorized redirect URIs**, use exatamente:
   - dev: `http://localhost:3000/api/auth/signin-google`
   - produção (quando existir): `https://<seu-domínio>/api/auth/signin-google`

   Use clients separados para dev e produção. **Authorized JavaScript origins** não é necessário (o fluxo é server-side).
4. Guarde o client id e o secret na API (nunca no git):

```bash
cd apps/api/src/Financas.Api
dotnet user-secrets set "Google:ClientId" "<client id>"
dotnet user-secrets set "Google:ClientSecret" "<client secret>"
```

A URI de redirect usa o host e a porta **do Next** (3000), não os da API: em dev o Next encaminha `/api/*` para a API (`rewrites` em `apps/web/next.config.ts`) e a API monta o `redirect_uri` a partir de `App:PublicOrigin` (`http://localhost:3000` em `appsettings.Development.json`). Se mudar a porta do Next, mude `App__PublicOrigin` e a URI no Google.

### 3. Usuários permitidos (seed SQL)

Não existe auto-cadastro nem tela de administração: só entra quem está na tabela `users`, ativo, com e-mail verificado no Google. Cadastre contas, boards, usuários e papéis pelo seed:

```bash
cp infra/postgres/seed.example.sql infra/postgres/seed.sql   # ignorado pelo git; edite com os dados reais
```

Escreva os e-mails em **minúsculas**. O script é idempotente (pode rodar de novo para adicionar ou ajustar pessoas). Rode **depois** das migrations (a API aplica as migrations ao subir em Development).

- Dev: `docker compose -f docker-compose.dev.yml exec -T postgres psql -U citros -d citros -v ON_ERROR_STOP=1 < infra/postgres/seed.sql`
- Produção: `psql "<connection string>" -v ON_ERROR_STOP=1 -f infra/postgres/seed.sql`

Para desativar alguém (derruba a sessão no request seguinte): `update users set is_active = false where email = '...';`

### 4. Rodar e testar o login

```bash
cd apps/api && dotnet run --project src/Financas.Api    # http://localhost:5080
cd apps/web && pnpm dev                                   # http://localhost:3000
```

Abra `http://localhost:3000` e use o botão "Entrar com Google". E-mail não cadastrado volta para `/login` com a mensagem de acesso negado. A sessão dura 7 dias e é renovada automaticamente quando faltam menos de 2 dias. Documentação da API (só em dev): `http://localhost:3000/api/docs`.

### Testes da API

```bash
cd apps/api && dotnet test      # precisa de Docker (Testcontainers sobe um Postgres 18)
```
