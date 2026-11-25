# 🍋 SPEC — Citros Flow

> **Estado do documento:** este spec descreve a arquitetura **atual**: Next.js + API ASP.NET Core + PostgreSQL, com login Google e login por senha. O projeto nasceu sobre o Supabase e migrou em etapas, conforme [docs/refactor-plan.md](../docs/refactor-plan.md) (concluída). As Fases 1–13 do roadmap abaixo são histórico e citam o Supabase por isso; as funcionalidades que elas entregaram continuam valendo.

---

## 1. Visão Geral

**Citros** é um sistema financeiro minimalista para fechamentos de caixa de instituições sem fins lucrativos. Prioriza visualização rápida e entrada ágil de dados via dashboard de tela única (single-screen no desktop, sem scroll global).

### Princípios Fundamentais
- **Clareza acima de tudo** — o usuário deve saber o saldo em menos de 2 segundos ao abrir o sistema
- **Zero fricção na entrada** — inserir uma transação deve exigir o mínimo de cliques possível
- **Fechamento mensal** — toda leitura financeira operacional acontece dentro de um mês/ano selecionado
- **Dados confiáveis** — a API é a única porta de acesso ao banco e aplica a autorização por papel em todo endpoint, de modo que cada usuário vê e altera apenas o que deve
- **IA-first, review-based** — código gerado por IA, revisado e aprovado por humano antes de merge

---

## 2. Stack Tecnológica

| Camada | Tecnologia | Versão |
|---|---|---|
| Front | Next.js (App Router) | 16.x |
| Linguagem (front) | TypeScript | 5.x |
| Runtime (front) | Node.js | 24 LTS |
| Package Manager | pnpm | 11.x |
| Backend | ASP.NET Core Web API (Minimal APIs por feature) | .NET 10 LTS |
| ORM e migrations | EF Core + Npgsql (`EFCore.NamingConventions`, snake_case) | 10.x |
| Banco de Dados | PostgreSQL (Docker Compose) | 18 |
| Autenticação | Login Google (OAuth) e login por e-mail + senha no backend, com cookie de sessão | — |
| Contrato da API | OpenAPI (`Microsoft.AspNetCore.OpenApi`) + `openapi-typescript` / `openapi-fetch` | — |
| Docs da API (dev) | Scalar (`Scalar.AspNetCore`), só em Development | 2.x |
| Testes (backend) | xUnit + Testcontainers (PostgreSQL) | — |
| UI Components | shadcn/ui + Radix UI | latest |
| Estilização | Tailwind CSS | 4.x |
| Temas | Provider próprio (`components/theme-provider.tsx`); `next-themes` removido | — |
| Linting/Format | Prettier + ESLint | — |
| Deploy | Fora do escopo atual (backlog: VPS com Docker Compose e Caddy) | — |

---

## 3. Arquitetura do Projeto

### 3.1 Estrutura de Pastas

Monorepo. O front fica em `apps/web`, a API em `apps/api`.

```
citros-flow/
├── apps/
│   ├── web/                              # Next.js
│   │   ├── src/
│   │   │   ├── app/
│   │   │   │   ├── (auth)/login/page.tsx # Formulário e-mail/senha, botão "Entrar com Google" + mensagens de erro
│   │   │   │   ├── (app)/
│   │   │   │   │   ├── layout.tsx        # Layout protegido (valida sessão via GET /api/me)
│   │   │   │   │   └── board/
│   │   │   │   │       ├── page.tsx      # Redirect para primeiro board ou empty state
│   │   │   │   │       └── [boardId]/
│   │   │   │   │           ├── actions.ts# Server Actions: chamam a API
│   │   │   │   │           └── page.tsx  # Dashboard principal (single-screen)
│   │   │   │   ├── share/[token]/page.tsx# Visualização pública view-only
│   │   │   │   ├── layout.tsx            # Root layout
│   │   │   │   └── page.tsx              # Redirect → login ou board
│   │   │   ├── components/
│   │   │   │   ├── ui/                   # shadcn/ui (gerado)
│   │   │   │   ├── panels/               # Entradas, Saídas, Fechamento
│   │   │   │   ├── modals/               # TransactionModal
│   │   │   │   ├── board/                # Header, calendário, import CSV, reservas, share...
│   │   │   │   └── layout/
│   │   │   ├── lib/
│   │   │   │   ├── api/schema.d.ts       # GERADO do OpenAPI (openapi-typescript). Não editar à mão
│   │   │   │   ├── api-client.ts         # openapi-fetch (server-only), repassa o Cookie
│   │   │   │   ├── money.ts              # formatação BRL e aritmética em centavos
│   │   │   │   └── utils.ts              # cn() etc.
│   │   │   └── proxy.ts                  # Checa presença do cookie de sessão; matcher exclui /api
│   │   └── next.config.ts                # rewrites: /api/:path* → API_INTERNAL_URL
│   └── api/                              # ASP.NET Core
│       ├── src/Financas.Api/
│       │   ├── Features/
│       │   │   ├── Auth/                 # login, callback, logout, me, allowlist
│       │   │   ├── Boards/               # lista, detalhe, BoardAccess (autorização)
│       │   │   ├── Transacoes/           # resumo do mês, sugestões, CRUD, categoria em massa, import Nubank
│       │   │   ├── Reservas/
│       │   │   └── Compartilhamento/     # criar link, leitura pública por token
│       │   ├── Data/                     # AppDbContext, Entities, Configurations, Migrations/
│       │   └── Program.cs
│       ├── openapi/openapi.json          # GERADO no dotnet build (commitado); fonte do gen:api
│       ├── tests/Financas.Api.Tests/
│       └── Financas.sln
├── infra/postgres/seed.example.sql       # accounts, boards, users e papéis iniciais
├── docker-compose.dev.yml                # postgres:18
├── .env.example
├── docs/refactor-plan.md
├── AGENTS.md                             # fonte das regras do agente
├── CLAUDE.md                             # contém apenas "@AGENTS.md"
└── README.md
```

### 3.2 Fluxo de Dados

```
PostgreSQL 18
      ↑ EF Core / Npgsql
API ASP.NET Core (Features/*, BoardAccess)    ←  cookie de sessão HttpOnly
      ↑ HTTP (API_INTERNAL_URL, com o Cookie repassado)
Server Components / Server Actions (apps/web/src/app)
      ↓
Client Components (components/)
      ↓
UI (shadcn/ui + Tailwind)
```

- O browser fala só com o Next, no mesmo origin. Chamadas a `/api/*` passam por rewrite do Next para a API (em dev) e, no futuro, por um reverse proxy. Não há CORS.
- Nenhum token chega ao browser: a sessão é um cookie HttpOnly emitido pela API.
- Totais, saldos e agrupamentos por categoria são calculados na API (SQL e C#). O front só formata.

---

## 4. Domain Models

### 4.1 Account (Conta)
Raiz da instituição. Pode ter múltiplos Boards.

```typescript
type Account = {
  id: string           // uuid
  name: string         // "Sede Central"
  created_at: string
}
```

### 4.2 Board (Núcleo)
Departamento ou projeto com gestão de caixa própria.

```typescript
type Board = {
  id: string
  account_id: string   // FK → accounts
  name: string         // "Departamento de Eventos"
  created_at: string
}
```

### 4.3 Transaction (Transação)
Registro unitário de movimentação financeira.

```typescript
type Transaction = {
  id: string
  board_id: string     // FK → boards
  type: 'entrada' | 'saida'
  amount: string       // decimal(12,2) como string no JSON: "1234.56"
  description: string
  category: string     // ex: "Doações", "Custos Operacionais"
  date: string         // date (YYYY-MM-DD)
  created_at: string
  created_by: string   // FK → users
  updated_at: string | null
  updated_by: string | null
}
```

### 4.4 PeriodScope (Escopo Mensal)
O fechamento do MVP é sempre calculado por mês/ano.

```typescript
type PeriodScope = {
  year: number          // ex: 2026
  month: number         // 1-12
  startDate: string     // primeiro dia do mês, YYYY-MM-DD
  endDate: string       // primeiro dia do mês seguinte, YYYY-MM-DD
}
```

Regras:
- Período padrão: mês atual na timezone da aplicação (`America/Sao_Paulo`)
- Navegação simples: mês anterior, mês seguinte e voltar para mês atual
- Queries usam intervalo fechado-aberto: `date >= startDate` e `date < endDate`
- Entradas, saídas e fechamento sempre usam apenas transações do período selecionado
- Saldo final do mês selecionado considera o saldo acumulado até o mês anterior
- Fórmula: `saldoFinal = saldoAnterior + entradasDoMes - saidasDoMes`
- Exceção: a **imagem de fechamento** (seção 7.10) pode usar um período customizado `[início, fim]` (datas inclusivas), porque as contas de um mês costumam ser pagas nos primeiros dias do mês seguinte. O dashboard continua sempre mensal.

### 4.5 UserRole (Permissão)
Vincula usuário a um board com um nível de acesso.

```typescript
type UserRole = {
  id: string
  user_id: string      // FK → users
  board_id: string | null // FK → boards (null = papel global, vale para todos os boards)
  role: 'admin' | 'tesoureiro' | 'junta_administrativa' | 'viewer'
}
// Uma pessoa pode ter VÁRIOS papéis no mesmo board (ex.: tesoureiro e junta_administrativa); as permissões se somam.
```

### 4.6 FinancialReserve (Reserva Financeira)
Valor guardado pelo board (dinheiro, caixinha, investimento), fora do fluxo de caixa operacional.

```typescript
type FinancialReserve = {
  id: string
  board_id: string     // FK → boards
  name: string          // "Caixinha", "Poupanca"
  amount: string         // decimal(12,2) como string no JSON
  created_by: string    // FK → users
  created_at: string
}
```

Regras:
- Reserva é um valor acumulado do board, sem filtro por mês/fechamento
- `admin` e `tesoureiro` podem cadastrar, editar e remover reservas
- `viewer` e link público apenas visualizam o total agregado
- Total de reservas é somente informativo: não entra no cálculo do saldo final
- Total aparece logo abaixo do saldo final no painel de Fechamento e na imagem de fechamento gerada

### 4.7 BoardShareLink (Link Público View-Only)
Permite visualizar um board sem login, apenas leitura.

```typescript
type BoardShareLink = {
  id: string
  board_id: string       // FK → boards
  token_hash: string     // hash do token público; token cru não fica salvo
  created_by: string     // FK → users
  created_at: string
  revoked_at: string | null
  expires_at: string | null
}
```

Regras:
- Link público abre rota `/share/[token]`
- Rota pública não exige login e não permite criar, editar ou remover transações
- Token cru só aparece no momento de geração/cópia
- Servidor valida token via hash antes de buscar board/transações
- MVP pode usar `expires_at = null`; revogação fica via `revoked_at`
- Gerar link exige permissão de mutação (`admin`/`tesoureiro`); o botão de compartilhar não aparece para `viewer`
- Revogar e listar links não têm UI no escopo atual (backlog); revogar é setar `revoked_at` no banco

### 4.8 User (Usuário Cadastrado)
Allowlist de login. Só e-mails presentes aqui conseguem entrar. Não há auto-cadastro.

```typescript
type User = {
  id: string
  email: string          // normalizado: trim + lower; único case-insensitive
  display_name: string | null
  is_active: boolean     // false = login negado e sessão derrubada no request seguinte
  password_hash: string | null  // hash PBKDF2 (PasswordHasher); null = só entra pelo Google
  created_at: string
  last_login_at: string | null
}
```

### 4.9 Dinheiro
- Valores monetários são `numeric(12,2)` no banco e `decimal` na API. Nunca `float`.
- No JSON, trafegam como **string** decimal (`"1234.56"`).
- Totais, saldos e grupos por categoria são calculados na API. O front só formata (BRL) e, quando precisa somar (ajustes temporários da imagem de fechamento), usa **centavos inteiros**.
- Entrada de formulário aceita vírgula (`"1.234,56"`); o front normaliza antes de enviar. A API exige valor maior que 0 e arredonda para 2 casas (`AwayFromZero`).
- O saldo anterior é um `SUM` com sinal feito no banco, sem limite de linhas.
- Escritas: o valor é arredondado para centavos (metade para longe do zero) e precisa ficar entre 0,01 e 9.999.999.999,99. Valor que arredonda para 0,00 é inválido. A API aceita string decimal com ponto (e número JSON); a vírgula do pt-BR é normalizada pelo front.
- Limites de texto: categoria até 80 caracteres; nome de reserva até 80; descrição até 120 na edição inline e até 500 (teto de segurança) ao criar, editar e importar, porque descrições importadas do Nubank passam de 120.

---

## 5. Schema do Banco (PostgreSQL)

O schema é criado e versionado pelas **migrations do EF Core** (`apps/api/src/Financas.Api/Data/Migrations/`). O SQL abaixo é a referência do resultado esperado. Nomes em snake_case, gerados por `UseSnakeCaseNamingConvention()`.

```sql
-- users: allowlist de login. Não existe auto-cadastro.
create table users (
  id uuid primary key default gen_random_uuid(),
  email text not null unique,          -- sempre gravado normalizado (trim + lower)
  display_name text,
  is_active boolean not null default true,
  password_hash text,                  -- null = sem login por senha; nunca a senha em texto
  created_at timestamptz not null default now(),
  last_login_at timestamptz,
  constraint ck_users_email_normalized check (email = lower(btrim(email)))
);
-- unique + check de normalização = unicidade case-insensitive (o EF não expressa índice em lower(email))

-- accounts
create table accounts (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  created_at timestamptz not null default now()
);

-- boards
create table boards (
  id uuid primary key default gen_random_uuid(),
  account_id uuid not null references accounts(id) on delete cascade,
  name text not null,
  created_at timestamptz not null default now()
);

-- transactions
create table transactions (
  id uuid primary key default gen_random_uuid(),
  board_id uuid not null references boards(id) on delete cascade,
  type text not null check (type in ('entrada', 'saida')),
  amount numeric(12,2) not null check (amount > 0),
  description text not null,
  category text not null,
  date date not null,
  created_by uuid not null references users(id),
  created_at timestamptz not null default now(),
  updated_by uuid references users(id),
  updated_at timestamptz
);

-- user_roles (board_id null = papel global)
create table user_roles (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references users(id) on delete cascade,
  board_id uuid references boards(id) on delete cascade,
  role text not null check (role in ('admin', 'tesoureiro', 'junta_administrativa', 'viewer')),
  unique nulls not distinct (user_id, board_id, role)   -- vários papéis por pessoa, mas não o mesmo duas vezes
);

-- tasks: quadro kanban do board (spec: tarefas-spec.md)
create table tasks (
  id uuid primary key default gen_random_uuid(),
  board_id uuid not null references boards(id) on delete cascade,
  name text not null,
  description text not null default '',
  status text not null check (status in ('a_fazer', 'em_andamento', 'concluida')),
  position integer not null,            -- ordem dentro da coluna (0 = topo)
  tags text[] not null default '{}',
  priority text check (priority in ('baixa', 'media', 'alta', 'urgente')),   -- opcional (nulo = sem prioridade; spec: tarefas-prioridade-spec.md)
  created_by uuid not null references users(id),
  created_at timestamptz not null default now(),
  updated_by uuid references users(id),
  updated_at timestamptz
);

create table task_assignees (
  task_id uuid not null references tasks(id) on delete cascade,
  user_id uuid not null references users(id) on delete cascade,
  primary key (task_id, user_id)
);

-- stock_items: tabela de estoque do board (spec: estoque-spec.md)
create table stock_items (
  id uuid primary key default gen_random_uuid(),
  board_id uuid not null references boards(id) on delete cascade,
  name text not null,
  status text not null check (status in ('em_estoque', 'a_caminho', 'comprar', 'comprar_com_urgencia')),
  assignee_id uuid references users(id) on delete set null,   -- um só responsável, opcional
  price numeric(12,2) check (price is null or price >= 0),
  details text not null default '',
  created_by uuid not null references users(id),
  created_at timestamptz not null default now(),
  updated_by uuid references users(id),
  updated_at timestamptz
);

-- board_share_links
create table board_share_links (
  id uuid primary key default gen_random_uuid(),
  board_id uuid not null references boards(id) on delete cascade,
  token_hash text not null unique,
  created_by uuid not null references users(id) on delete cascade,
  created_at timestamptz not null default now(),
  revoked_at timestamptz,
  expires_at timestamptz
);

-- financial_reserves
create table financial_reserves (
  id uuid primary key default gen_random_uuid(),
  board_id uuid not null references boards(id) on delete cascade,
  name text not null,
  amount numeric(12,2) not null check (amount > 0),
  created_by uuid not null references users(id),
  created_at timestamptz not null default now()
);

-- índices do MVP
create index transactions_board_date_idx on transactions (board_id, date);
create index transactions_board_category_idx on transactions (board_id, category);
create index transactions_board_description_idx on transactions (board_id, description);
create index user_roles_user_id_idx on user_roles (user_id);
create index board_share_links_board_id_idx on board_share_links (board_id);
create index financial_reserves_board_id_idx on financial_reserves (board_id);
```

Notas:
- `transactions.updated_at` e `updated_by` são preenchidos pela API em toda edição (PUT, descrição, categoria em massa). O `supabase/schema.sql` antigo não tinha essas colunas.
- `created_by` e `updated_by` apontam para `users` (antes, `auth.users` do Supabase).
- Nomes de PKs, FKs, índices e constraints são gerados pelo EF Core (`pk_*`, `fk_*`, `ix_*`) e podem diferir dos nomes ilustrativos do SQL acima; a estrutura é a mesma. O `unique nulls not distinct` de `user_roles` é um índice único com `NULLS NOT DISTINCT`.
- Todo valor monetário é `numeric(12,2)` no banco e `decimal` no C#. Nunca `float` nem `double`.

---

## 6. Autorização (na API)

O RLS do Supabase deixa de existir. A API é a **única** porta de acesso ao banco, e a autorização é centralizada em um serviço `BoardAccess` usado por todo endpoint de board.

### Regras

| Papel | Leitura | Escrita (transações, reservas, links) |
|---|---|---|
| `admin` ou `tesoureiro` no board | ✅ boards vinculados | ✅ boards vinculados |
| `admin` ou `tesoureiro` global (`board_id` null) | ✅ todos os boards | ✅ todos os boards |
| `viewer` no board | ✅ board vinculado | ❌ |
| `viewer` global | ✅ todos os boards | ❌ |
| `junta_administrativa` no board ou global | ✅ (em leitura, como o `viewer`, no financeiro) | ❌ no financeiro; ✅ em tarefas (e estoque) |
| Link público view-only | ✅ apenas via `/api/share/{token}/...` | ❌ |

- `CanView(boardId)`: o usuário tem qualquer papel no board **ou** qualquer papel global. (Diferença em relação ao RLS antigo, em que só o `admin` global lia tudo e o `tesoureiro` global escrevia sem poder ler.)
- `CanMutate(boardId)`: **financeiro** (transações, reservas, links). O usuário tem papel `admin` ou `tesoureiro` no board, ou global.
- `CanManageTasks(boardId)`: **tarefas** (e estoque, no futuro). O usuário tem papel `admin` ou `junta_administrativa` no board, ou global. Quem tem qualquer papel no board **lê** o quadro de tarefas.
- **Papéis se somam:** a mesma pessoa pode ter vários papéis no mesmo board (e globais); `CanMutate` e `CanManageTasks` são calculados sobre todos eles.
- `CanManageBoards`: o usuário tem papel `admin` **global** (`board_id` nulo). Só ele cria, renomeia e remove boards e vê a tela de gestão (seção 7.12). Admin de um board específico, `tesoureiro` e `viewer` não gerenciam boards.
- Board que o usuário não pode ver responde **404**, como o `notFound()` atual. Usuário que vê o board mas não pode mutar recebe **403**.
- Toda escrita checa `CanMutate` explicitamente. Nenhuma depende de efeito colateral do banco.
- `created_by` e `updated_by` vêm sempre da sessão, nunca do corpo da requisição.
- A **lista** de reservas (nomes e valores) só é devolvida a `admin`/`tesoureiro` (403 para `viewer`). Viewer e link público veem apenas o total, que vem no resumo do mês (seção 4.6).
- Todo endpoint, exceto `GET /api/health`, o login e o link público, exige sessão. Sem sessão: 401 em JSON, sem redirect.
- **CSRF:** cookie `SameSite=Lax`, e métodos não-GET com header `Origin` presente e diferente de `App:PublicOrigin` são rejeitados.

### Link público view-only
- O token público é validado apenas na API, por hash (SHA-256). O token cru nunca é salvo.
- `GET /api/share/{token}/months/{yyyy-MM}` é anônimo e devolve somente:
  - nome do board;
  - transações do mês selecionado;
  - totais de entradas, saídas, saldo anterior, saldo final e total de reservas.
- Não devolve sugestões, nem a lista de reservas.
- Não existe endpoint de mutação acessível sem sessão.
- Token inexistente, revogado (`revoked_at`) ou expirado (`expires_at`) responde 404.

---

## 7. Interface e Layout

### 7.1 Dashboard (Tela Principal)

```
┌─────────────────────────────────────────────────────┐
│ Sidebar │ Header: Board · título | Mês/Ano | Share   │
│ (boards,├──────────────┬──────────────┬─────────────┤
│   ENTRADAS   │    SAÍDAS    │     FECHAMENTO        │
│  (scroll)    │  (scroll)    │     (estático)        │
│              │              │                       │
│ — Doações —  │ — Operac. — │  Total Entradas       │
│ [transação]  │ [transação]  │  Total Saídas         │
│ [edit/del]   │ [edit/del]   │  ─────────────        │
│              │              │  Saldo Final          │
│ — Outros —   │ — Outros —  │                       │
│ [transação]  │ [transação]  │  [+ Nova Transação]   │
│              │              │                       │
└──────────────┴──────────────┴───────────────────────┘
```

### 7.2 Regras de Layout
- `body`: `overflow-hidden`, altura `100vh`
- Painéis de Entradas e Saídas: `overflow-y-auto` com scroll isolado
- Painel de Fechamento: estático, sem scroll
- Agrupamento por `category` dentro de cada painel com separador visual
- Responsividade: mobile-first, colunas colapsam em telas pequenas
- **Sidebar lateral** (spec em `.specs/sidebar-spec.md`) substitui o menu hambúrguer: troca de board, módulos do board (Dashboard, Fluxo de caixa, Gestão de tarefas, Gestão de estoque), Gerenciar boards (admin global), tema e Sair
- Header (faixa azul) concentra o contexto da tela: botão da sidebar, board e título, e as ações da tela (mês/ano e link público no fluxo de caixa)
- Header não exibe email do usuário no MVP
- Em telas largas o navegador de mês aparece completo; em faixas estreitas (sidebar aberta, celular) vira um botão de calendário com menu (mês atual, anterior, próximo e hoje), e o botão Compartilhar fica só com o ícone. A escolha é pela largura do cabeçalho, não da janela
- Área principal contém apenas informações e ações do board atual
- Sem scroll global no dashboard autenticado; scroll fica isolado nos painéis
- Com a sidebar aberta o conteúdo comprime na horizontal. O grid escolhe pela largura do **conteúdo**: ≥ 56rem três colunas; de 42rem a 56rem entradas | saídas com o fechamento embaixo; abaixo disso empilhado

### 7.3 Escopo Mensal
- Componente `MonthNavigator` fica no Header do fluxo de caixa
- Controles mínimos: mês anterior, mês seguinte, mês atual
- URL carrega período selecionado via query string: `/board/[boardId]/fluxo-de-caixa?month=YYYY-MM` (o endereço antigo `/board/[boardId]?month=YYYY-MM` redireciona para ele)
- Sem query string, usar mês atual
- Navegação de mês não altera board selecionado
- Todas as queries de transações recebem `boardId`, `startDate` e `endDate`

### 7.4 Modal de Transação
- Componente `Dialog` do shadcn/ui
- Campos: Tipo (entrada/saída), Valor, Descrição, Categoria, Data
- Validação client-side antes do submit
- Feedback visual de loading e erro
- Modal serve para criar e editar transações
- Edição abre modal preenchido com dados atuais
- Após salvar/remover, dashboard revalida o mês selecionado

### 7.5 Categoria com Combobox
- Campo `Categoria` funciona como input com dropdown filtrável
- Opções vêm de categorias já usadas no board, preferencialmente no histórico completo do board
- Digitação filtra opções por substring case-insensitive
- Se valor digitado não existir, salvar transação com nova categoria
- MVP não precisa de CRUD separado de categorias
- Normalização visual: trim, colapsar espaços duplicados e preservar capitalização digitada pelo usuário

### 7.6 Descrição com Sugestões
- Campo `Descrição` funciona como input com dropdown filtrável
- Opções vêm de descrições anteriores do board
- Digitação filtra opções por substring case-insensitive
- Selecionar sugestão apenas preenche o campo; não cria entidade separada
- Objetivo: acelerar lançamentos recorrentes

### 7.7 Ações em Transações
- Cada transação exibida deve ter ações discretas: editar e remover
- Editar abre `TransactionModal` em modo edição
- Remover pede confirmação antes de excluir
- Remoção e edição são autorizadas pela API conforme o papel do usuário (seção 6)
- Viewer não vê ações de mutação
- Descrição pode ser editada rapidamente direto no dashboard
- Ao clicar/focar no campo de descrição, o campo entra em modo edição inline
- Modo edição inline mostra ícones discretos no canto direito do input:
  - confirmar alteração
  - cancelar modificação
- Confirmar salva apenas `description`; categoria, data, tipo e valor permanecem inalterados
- Cancelar restaura a descrição original sem chamada ao servidor
- Edição inline respeita as mesmas permissões de mutação (`admin` e `tesoureiro`)
- Categoria pode ser alterada em lote por seleção múltipla de transações
- Seleção múltipla deve usar checkboxes discretos por linha e controle de selecionar/limpar seleção no painel
- Clicar no card da transação também alterna seleção, exceto quando o clique ocorrer em inputs, botões ou ações interativas
- Ao selecionar transações, exibir barra contextual com:
  - quantidade selecionada
  - campo de categoria com sugestões já existentes
  - ação para aplicar categoria
  - ação para limpar seleção
- Aplicar categoria atualiza apenas `category` das transações selecionadas
- Após aplicar, seleção é limpa e dashboard revalida
- Viewer não vê checkboxes nem barra de edição em lote

### 7.8 Link Público View-Only
- Header mostra comando para gerar/copiar link público do board atual, apenas para `admin` e `tesoureiro` (oculto para `viewer`)
- Link abre `/share/[token]`
- Página pública reaproveita visualização do dashboard, mas sem:
  - user menu
  - botão nova transação
  - editar/remover
  - board selector
  - gerar imagem de fechamento
- Página pública mantém navegação mensal simples; o botão "Hoje" volta para o mês **atual**, não para o mês exibido
- Se token inválido, expirado ou revogado: renderizar `notFound()`

### 7.9 Importação de Extrato CSV Nubank
- Objetivo: acelerar lançamentos usando extrato Nubank em CSV enviado pelo usuário
- Arquivo esperado: CSV com cabeçalho `Data,Valor,Identificador,Descrição`
- Exemplo de linha:
  - `01/05/2026,180.00,69f48267-ebdd-4b8f-9b4d-6185ecbdce00,Transferência recebida pelo Pix - ...`
- Campos usados pelo sistema:
  - `Data` → `transactions.date`
  - `Valor` → `transactions.amount`
  - `Descrição` → `transactions.description`
- Campo `Identificador` é ignorado no MVP
- Valor positivo cria transação `entrada`
- Valor negativo cria transação `saida`
- `amount` é salvo sempre como valor absoluto
- Categoria padrão para linhas importadas: `Dados importados`
- MVP não faz deduplicação; importar o mesmo arquivo novamente cria novas transações
- Importação é autorizada pela API e só aparece para roles com permissão de mutação (`admin` e `tesoureiro`)
- Após importação, dashboard revalida; transações aparecem nos meses correspondentes às datas do CSV
- Erros de formato devem retornar feedback claro sem quebrar o dashboard
- A importação é tudo ou nada: uma linha inválida rejeita o arquivo inteiro, e as inserções acontecem em uma única transação de banco. Datas inexistentes (31/02) invalidam a linha; arquivos acima de 5 MB são recusados
- O parser roda na API (`POST /api/boards/{boardId}/transactions/import/nubank`, multipart)

### 7.10 Imagem de Fechamento Mensal
- Objetivo: gerar arquivo de imagem (`.png`) com resumo mensal para compartilhamento externo
- Geração acontece no browser; não cria registro no banco e não exige dependência nova
- Botão fica no painel de Fechamento como ação operacional
- Antes de gerar a imagem, abrir modal com:
  - **período do fechamento**: data de início e data de fim (ambas inclusivas), pré-preenchidas com o primeiro e o último dia do mês selecionado
  - entrada extra opcional: categoria + valor
  - saída extra opcional: categoria + valor
- **Por que período customizado:** as contas de um mês costumam ser pagas entre o dia 1 e o dia 5 do mês seguinte (dia 1, ou 2, 3 ou 4 quando há feriado). O fechamento real do mês precisa poder terminar nesse dia de pagamento (ex.: 02/10 a 01/11).
- Ao gerar, o modal busca o resumo do período na API (`GET /api/boards/{boardId}/periods?start=yyyy-MM-dd&end=yyyy-MM-dd`, ver abaixo). Entradas, saídas, saldo anterior e total de reservas vêm da API; o front só soma os ajustes temporários, em centavos.
- **Saldo anterior do período** = soma com sinal de todas as transações com data anterior ao início. Assim, se o período seguinte começa no dia seguinte ao fim do anterior, os saldos encadeiam.
- Valores temporários entram apenas no cálculo/arte do fechamento gerado
- Valores temporários não são salvos no DB e não alteram o dashboard
- Entrada extra soma nas entradas; saída extra soma nas saídas
- Imagem deve conter:
  - cabeçalho: `Fluxo de caixa`, nome do board, mês/ano; quando o período não é o mês inteiro, o subtítulo mostra também o período (`Outubro de 2026 (02/10/2026 a 01/11/2026)`)
  - lado esquerdo: entradas por categoria
  - lado direito: saídas por categoria, incluindo ajustes temporários
  - rodapé/resumo: saldo anterior, entradas, saídas, saldo final, total de reservas financeiras (informativo)
- O saldo final da imagem usa: `saldo anterior + entradas ajustadas - saídas ajustadas`
- Total de reservas financeiras aparece logo abaixo do saldo final e não entra na fórmula do saldo final
- Categorias repetidas devem ser agregadas antes de desenhar a imagem
- O arquivo gerado deve ser baixado com nome legível contendo board e período

**Endpoint `GET /api/boards/{boardId}/periods?start=yyyy-MM-dd&end=yyyy-MM-dd`:**
- Mesmo acesso do resumo mensal (qualquer papel que enxerga o board; board inacessível = 404)
- `start` e `end` são datas inclusivas no formato exato `yyyy-MM-dd`; a API consulta `date >= start` e `date < end + 1 dia`
- 400 (ProblemDetails com `title` legível) se: formato inválido, `end < start` ou período maior que 366 dias
- Resposta: `start`, `end`, `previousBalance`, `entradasTotal`, `saidasTotal`, `balance`, `reservesTotal`, `transactionCount`, `entradas`, `saidas` (mesmos grupos por categoria do resumo mensal)
- O resumo mensal (`/months/{month}`) não muda; os dois compartilham a mesma consulta

### 7.11 Reservas Financeiras
- Objetivo: registrar valores guardados pelo board (dinheiro, caixinha, investimentos) fora do fluxo de caixa operacional
- Botão "Reservas financeiras" fica no painel de Fechamento, visível apenas para `admin` e `tesoureiro`
- Modal permite cadastrar, editar e remover reservas (campos: nome + valor)
- Remover reserva pede confirmação antes de excluir
- Total agregado das reservas aparece logo abaixo do saldo final no painel de Fechamento, para todos os papéis (incluindo `viewer`) e no link público
- Total de reservas é somente informativo: não entra no cálculo do saldo final do fechamento
- Reserva é um valor acumulado do board, sem filtro por mês; o mesmo total aparece em qualquer mês selecionado
- Total de reservas também aparece na imagem de fechamento mensal gerada

### 7.12 Gestão de Boards
- Cada board é um fluxo de caixa independente (transações, reservas e links próprios). A aplicação pode ter vários boards.
- **Quem gerencia:** só `admin` global (seção 6, `CanManageBoards`). Os demais papéis não veem a tela nem os endpoints (403).
- **Tela `/boards`** (só para quem gerencia; os outros recebem 404 da própria página): lista os boards com nome, data de criação e a quantidade de transações, reservas e links públicos (não revogados) de cada um. Ações:
  - **Novo board:** modal com o nome.
  - **Editar:** modal para renomear.
  - **Abrir:** leva a `/board/{id}`.
  - **Remover:** modal de confirmação que mostra o que será apagado (transações, reservas e links públicos) e só libera o botão depois de digitar o nome exato do board. A remoção é **definitiva**: o banco apaga em cascata transações, reservas, links públicos e os papéis que eram só daquele board. Usuários não são apagados.
- **Navegação:** a sidebar tem o seletor de board e, para quem pode gerenciar, o item "Gerenciar boards". Sem boards, `/board` leva o admin global a `/boards` (os demais veem a mensagem "Nenhum board vinculado").
- **Nome:** obrigatório, sem espaços nas pontas, até 80 caracteres, **único** (sem distinguir maiúsculas). Duplicado responde 409.
- **Conta:** todo board precisa de uma conta (`accounts`), mas não há tela de contas. A API usa a conta mais antiga e cria a "Conta principal" se não existir nenhuma. A tela nunca fala em conta.
- Quem cria um board não ganha papel nele: o admin global já enxerga todos os boards. Dar acesso a outros usuários continua sendo por seed SQL (papéis).

**Endpoints** (todos exigem sessão; sem papel de admin global: 403, sem revelar se o board existe):
- `GET /api/admin/boards`: lista com `id`, `name`, `createdAt`, `transactionCount`, `reserveCount`, `shareLinkCount`, por nome (pt-BR).
- `POST /api/boards` com `{ name }`: 200 com `{ id, name }`; 400 nome inválido; 409 nome já usado.
- `PUT /api/boards/{boardId}` com `{ name }`: 200 com `{ id, name }`; 400, 404 (board inexistente) e 409 (nome de **outro** board; trocar só maiúsculas do próprio nome é permitido).
- `DELETE /api/boards/{boardId}`: 204; 404 se não existe.
- `GET /api/me` passa a devolver `canManageBoards` (o front usa para mostrar o link e proteger `/boards`).

### 7.13 Gestão de Tarefas (kanban)
- Quadro por board em `/board/{id}/tarefas` com três colunas fixas: **A fazer**, **Em andamento**, **Concluída**. Spec completa em `.specs/tarefas-spec.md`.
- Tarefa: **nome** e **status** obrigatórios; descrição, responsáveis (pessoas ativas com papel no board ou global) e tags (texto livre, com sugestões) opcionais.
- **Prioridade** opcional (`Baixa`, `Média`, `Alta`, `Urgente`; selo no cartão, filtro e "Ordenar por prioridade" só na tela; spec em `.specs/tarefas-prioridade-spec.md`). "Minhas tarefas" do dashboard lista as mais prioritárias primeiro.
- Mover entre colunas e reordenar por arrastar (`@dnd-kit`, com mouse, toque e teclado) ou pelo menu "Mover para". O arrastar é otimista.
- Quem tem qualquer papel no board vê o quadro em leitura; **só `admin` e `junta_administrativa`** criam, editam, movem e removem (`CanManageTasks`, seção 6).
- API: `GET/POST /api/boards/{id}/tasks`, `PUT/DELETE .../tasks/{taskId}`, `PATCH .../tasks/{taskId}/move`, `GET .../task-assignees` (só para quem gerencia).

### 7.14 Estoque (tabela)
- Tabela por board em `/board/{id}/estoque` com **Item**, **Status**, **Responsável**, **Preço** e **Detalhes**. Spec completa em `.specs/estoque-spec.md`.
- Status fixos: `Em estoque`, `A caminho`, `Comprar`, `Comprar com urgência`. Obrigatórios: só Item e Status. Responsável é uma pessoa (a mesma lista das tarefas); Preço em reais, sem quantidade e sem total.
- Busca pelo nome (sem acento e sem maiúsculas), filtro por status com contagem, ordenação pelo cabeçalho (padrão: urgência e nome) e troca de status na própria linha.
- Mesma regra de permissão das tarefas: qualquer papel no board lê; só `admin` e `junta_administrativa` (`CanManageTasks`) escrevem.
- API: `GET/POST /api/boards/{id}/stock-items`, `PUT/DELETE .../stock-items/{itemId}`, `PATCH .../stock-items/{itemId}/status`.

### 7.15 Dashboard (tela inicial)
- Primeira tela do board: `/board/{id}/dashboard`. O login, `/board` e a troca de board fora de um módulo levam a ela. Spec completa em `.specs/dashboard-spec.md` (modelo: tela inicial do `igb-sistema`).
- Blocos: **Caixa do mês** (cartões de Saldo final, Entradas, Saídas e Reservas + gráfico de colunas de entradas × saídas dos últimos 6 meses), **Tarefas** (contagem por coluna e "Minhas tarefas") e **Estoque** (contagem por status e "Precisa de ação"). Cada bloco tem o link "Ver tudo" para o módulo.
- Qualquer papel no board lê. Os totais vêm calculados da API (`GET /api/boards/{id}/dashboard?month=yyyy-MM`), com os mesmos números do resumo mensal; o front só formata. Gráfico em CSS, com tabela equivalente para leitor de tela.
- `/board/{id}?month=...` (endereço antigo do fluxo de caixa) continua levando ao fluxo de caixa.

---

## 8. Autenticação

- Dois jeitos de entrar, ambos no backend ASP.NET Core e ambos terminando no mesmo cookie de sessão: **Google** (OAuth) e **e-mail + senha**. Não há recuperação de senha nem tela para trocar senha: a senha é definida por comando (ver abaixo).
- A sessão é um cookie `citros_session`: HttpOnly, `SameSite=Lax`, `Secure` conforme o request, válido por 7 dias. Ele só é **reemitido (Set-Cookie) quando faltam menos de 2 dias** para vencer; fora dessa janela a API não manda Set-Cookie. O sliding padrão do ASP.NET (renova após metade da validade) fica desligado.
- **Sem auto-cadastro.** Só entra quem já está na tabela `users`.

### Fluxo
1. `/login` mostra o botão "Entrar com Google", que leva a `/api/auth/login?returnUrl=<caminho>`. O `returnUrl` só é aceito se for um caminho relativo (começa com `/` e não com `//`).
2. A API redireciona para o Google. O callback é `/api/auth/signin-google`.
3. Antes de criar a sessão, a API:
   - exige `email_verified = true` no retorno do Google;
   - normaliza o e-mail (trim + lower) e o procura em `users`, comparação case-insensitive, exigindo `is_active = true`.
4. Falha na verificação ou usuário não encontrado: **nenhuma sessão é criada** e o usuário volta para `/login?error=unverified` (e-mail não verificado) ou `/login?error=not_allowed` (não cadastrado ou inativo, sem distinguir). Falha do próprio Google (consentimento negado, state inválido) volta com `/login?error=google_failed`.
5. Sucesso: a API grava `last_login_at`, emite o cookie (com `sub = users.id`) e redireciona para o `returnUrl`.
6. A cada request autenticado, a API revalida `users.is_active`. Usuário desativado perde a sessão no request seguinte.
7. Logout: `POST /api/auth/logout`. Usuário atual: `GET /api/me`.

### Login por e-mail e senha
- Vale para **qualquer usuário de `users` que tenha `password_hash`**, em dev e em produção. Quem não tem senha continua só com o Google. O papel (`admin`, `tesoureiro`, `viewer`) segue mandando nas permissões.
- `/login` tem um formulário (e-mail, senha) que faz `POST /api/auth/password-login` (form-urlencoded, direto do browser, mesmo origin) com os campos `email`, `password` e `returnUrl`. O `returnUrl` passa pela mesma validação do Google.
- A API normaliza o e-mail (trim + lower), exige `is_active = true` e confere a senha com o `PasswordHasher` do ASP.NET (sem dependência nova). Sucesso: grava `last_login_at`, emite o mesmo cookie `citros_session` e responde com redirect para o `returnUrl`.
- Falha: nenhuma sessão é criada e o usuário volta para `/login?error=invalid_credentials` (e-mail inexistente, inativo, sem senha ou senha errada, sem distinguir). A resposta demora o mesmo nos casos de falha (a API sempre faz uma verificação de hash), para não revelar se o e-mail existe.
- **Limite de tentativas:** 5 falhas por e-mail em 15 minutos bloqueiam novas tentativas desse e-mail pelo resto da janela (`/login?error=too_many_attempts`), mesmo com a senha certa. Um login com sucesso zera o contador. O controle fica em memória da API (uma réplica). Contrapartida aceita: quem sabe o e-mail do admin consegue travar o login dele por 15 minutos, mas não consegue adivinhar a senha.
- **Definir ou trocar a senha:** comando da API, sem tela. `dotnet run --project src/Financas.Api -- set-password <email>` (dev) ou `dotnet Financas.Api.dll set-password <email>` (container). Lê a senha duas vezes no terminal, sem eco (ou da entrada padrão, se não for um terminal), exige no mínimo 12 caracteres, grava só o hash e **não cria usuário**: o e-mail precisa existir em `users` (seed). Termina com código 0 em sucesso e diferente de 0 em erro.
- A senha nunca é registrada em log, nem guardada em arquivo ou variável de ambiente.

### Manter a sessão viva no front
- As chamadas do servidor do Next à API não repassam `Set-Cookie` ao browser (Server Components não gravam cookies), então a renovação precisa vir de uma chamada feita **pelo browser**.
- O componente `SessionKeepAlive`, no layout `(app)`, chama `GET /api/me`: ao carregar a página e quando a aba volta a ficar visível depois de mais de 10 minutos.
- `401` (sessão vencida ou usuário desativado) leva a `/login?error=session_expired&next=<tela atual>`, com a mensagem "Sua sessão expirou". Falha de rede ou `5xx` é ignorada: a API reiniciando não é motivo para deslogar.
- A tela `/login` entende os códigos `not_allowed`, `unverified`, `google_failed`, `invalid_credentials`, `too_many_attempts` (vêm da API) e `session_expired` (vem do front).
- O `proxy.ts` só olha se o cookie existe e **não** redireciona `/login` para `/board`: com um cookie presente mas inválido isso causaria loop. Quem decide é a página de login, perguntando à API.

### Mesmo origin
- O callback fica sob `/api` e a URI de redirect usa o host e a porta do **Next**: dev `http://localhost:3000/api/auth/signin-google`.
- A API monta URLs absolutas a partir de `App:PublicOrigin`, não dos headers do proxy.
- `proxy.ts` (Next 16) só verifica se o cookie existe e **exclui `/api` do matcher**. A validação real é feita no layout `(app)` com `GET /api/me`.

### Rotas públicas (front)
`/login` e `/share/*`. Todo o resto exige sessão.

---

## 9. Variáveis de Ambiente

Ver `.env.example`.

```bash
# Postgres (docker-compose.dev.yml)
POSTGRES_USER=
POSTGRES_PASSWORD=
POSTGRES_DB=
POSTGRES_PORT=                 # porta no host; 5434 se a 5432 estiver ocupada

# API (appsettings / user-secrets)
ConnectionStrings__Default=
Google__ClientId=              # apenas server-side (dev: dotnet user-secrets)
Google__ClientSecret=          # apenas server-side
App__PublicOrigin=             # origin público do Next, ex.: http://localhost:3000
Database__MigrateOnStartup=    # true em dev
DataProtection__KeysPath=      # opcional em dev; volume persistente em produção

# Web
API_INTERNAL_URL=              # ex.: http://localhost:5080 (rewrites e fetch server-side)
```

---

## 10. RBAC — Controle de Acesso

| Role | Descrição | Escopo |
|---|---|---|
| `admin` | Lê e altera transações, reservas e links | Board vinculado ou global |
| `tesoureiro` | Lê e altera transações, reservas e links | Board vinculado ou global |
| `junta_administrativa` | Lê o board; altera tarefas (e estoque, no futuro) | Board vinculado ou global |
| `viewer` | Apenas visualiza o fechamento | Board vinculado ou global |

Uma pessoa pode ter mais de um papel (ex.: `tesoureiro` e `junta_administrativa` no mesmo board). Financeiro: `admin` e `tesoureiro` têm as mesmas permissões sobre os dados do board. Tarefas e estoque: `admin` e `junta_administrativa`. Um papel com `board_id` nulo vale para todos os boards (regras detalhadas na seção 6).

Boards são gerenciados por uma tela própria, só para `admin` global (seção 7.12). Não há tela para usuários, contas e papéis: são cadastrados por seed SQL (`infra/postgres/seed.example.sql`), inclusive em produção.

---

## 11. Convenções de Desenvolvimento

### Fluxo IA-First / Review-Based
1. **Prompt** → IA gera o código (feature completa ou componente)
2. **Review** → desenvolvedor revisa linha a linha
3. **Ajuste** → correções manuais ou novo prompt refinado
4. **Commit** → apenas após aprovação humana

### Acesso ao Banco de Dados

**Regra:** só a API acessa o PostgreSQL, via EF Core, dentro de `apps/api/src/Financas.Api/Features/*` e `Data/`. O front **nunca** fala com o banco.

- O front consome a API por meio do cliente gerado (`apps/web/src/lib/api/schema.d.ts` + `lib/api-client.ts`, `server-only`). Componentes não chamam `fetch` para a API diretamente.
- `lib/api/schema.d.ts` é gerado do OpenAPI (`pnpm gen:api`) e **não se edita à mão**. O `apps/api/openapi/openapi.json` é gerado no `dotnet build` e commitado.
- Toda chamada server-side à API repassa o header `Cookie` da requisição (centralizado em `api-client.ts`).
- Endpoints de board passam pelo `BoardAccess` (seção 6). Nenhuma regra de autorização fica só no front.
- Regras de negócio e cálculos financeiros ficam na API. O front não soma valores monetários, exceto em centavos inteiros nos ajustes temporários da imagem de fechamento.
- Mudanças de schema só por migration do EF Core.

### Nomenclatura
- Componentes: PascalCase (`TransactionModal.tsx`)
- Hooks: camelCase com prefixo `use` (`useTransactions.ts`)
- Funções utilitárias: camelCase (`formatCurrency`)
- Variáveis de ambiente: SCREAMING_SNAKE_CASE

### Commits
Seguir Conventional Commits:
```
feat: adiciona modal de transação
fix: corrige cálculo de saldo no FechamentoPanel
chore: atualiza dependências
```

---

## 12. Roadmap de Implementação

### Fase 1 — Fundação ✅
- [x] Configurar Supabase (projeto, schema, RLS)
- [x] Configurar variáveis de ambiente
- [x] Implementar autenticação (login/logout)
- [x] Login por e-mail + senha: coluna `users.password_hash` (migration), `POST /api/auth/password-login`, limite de tentativas, comando `set-password`, formulário em `/login`, testes
- [x] Proxy de proteção de rotas
- [x] Tipos TypeScript exportados do Supabase

### Fase 2 — Dashboard Core ✅
- [x] Layout single-screen (100vh, 3 colunas)
- [x] EntradasPanel com scroll isolado e agrupamento por categoria
- [x] SaidasPanel com scroll isolado e agrupamento por categoria
- [x] FechamentoPanel com totais e saldo
- [x] Responsividade para mobile, empilhando os panels, mas mantendo o layout single-screen

### Fase 3 — Criar Transações ✅
- [x] TransactionModal (Dialog shadcn)
- [x] Formulário com validação
- [x] Insert no Supabase com RLS
- [x] Atualização em tempo real dos painéis

### Fase 4 — Fechamento Mensal
- [x] Atualizar query `listTransactionsByBoard` para receber `startDate` e `endDate`
- [x] Criar helper local para parse/format de `YYYY-MM`
- [x] Criar `MonthNavigator` no cabeçalho local do dashboard
- [x] Persistir mês selecionado em query string `?month=YYYY-MM`
- [x] Calcular entradas, saídas e saldo apenas para o mês selecionado
- [x] Considerar saldo acumulado anterior no saldo final do mês vigente
- [x] Garantir que `/board` redireciona para primeiro board sem perder mês atual
- [x] Validar mobile/desktop sem scroll global

### Fase 5 — Editar e Remover Transações
- [x] Expandir `transactions.ts` com `updateTransactionForCurrentUser` e `deleteTransactionForCurrentUser`
- [x] Ajustar RLS para `admin` e `tesoureiro` poderem editar/remover transações do board
- [x] Reaproveitar `TransactionModal` em modo criação e edição
- [x] Adicionar ações discretas em cada item de transação
- [x] Adicionar confirmação antes de remover
- [x] Revalidar rota preservando `boardId` e `month`
- [x] Esconder ações de mutação para `viewer`

### Fase 6 — Entrada Rápida com Sugestões
- [x] Criar query de sugestões de categorias por board
- [x] Criar query de sugestões de descrições por board
- [x] Implementar combobox de Categoria com filtro por digitação
- [x] Implementar dropdown de Descrição com filtro por digitação
- [x] Ao salvar categoria nova, persistir na própria transação
- [x] Não criar tabela de categorias no MVP, salvo decisão explícita

### Fase 7 — Header Operacional
- [x] Criar/ajustar `Header` no layout protegido
- [x] Remover email do Header
- [x] Mover board atual/selector simples para Header operacional
- [x] Mover navegação mês/ano para Header operacional
- [x] Desktop: instituição+board à esquerda, calendário no centro, compartilhar+hamburguer à direita
- [x] Mobile: instituição+board à esquerda, calendário ícone+share ícone+hamburguer à direita
- [x] Menu de calendário mobile com mês anterior, próximo mês e hoje
- [x] Menu hamburguer com opção `Sair`
- [x] Exibir botão de compartilhar no Header operacional
- [x] Remover contexto duplicado da área principal
- [x] Deixar main apenas com painéis e ações do board

### Fase 8 — Importar Extrato CSV Nubank
- [x] Criar parser local para CSV Nubank
- [x] Aceitar upload de arquivo `.csv` pelo usuário
- [x] Ignorar coluna `Identificador`
- [x] Converter `Data` de `DD/MM/YYYY` para `YYYY-MM-DD`
- [x] Converter `Valor` positivo para `entrada` e negativo para `saida`
- [x] Salvar `amount` como valor absoluto
- [x] Salvar categoria como `Dados importados`
- [x] Inserir transações em lote via Supabase respeitando RLS
- [x] Revalidar dashboard após importação
- [x] Exibir feedback de sucesso/erro para o usuário
- [x] Não implementar deduplicação nesta fase

### Fase 9 — Edição Rápida de Descrição
- [x] Criar server action para atualizar apenas `description`
- [x] Criar query dedicada para update parcial da descrição
- [x] Renderizar descrição como input editável no dashboard
- [x] Ao focar/clicar, exibir botões de confirmar e cancelar no canto direito do input
- [x] Confirmar salva descrição e revalida dashboard
- [x] Cancelar descarta alteração local
- [x] Esconder edição inline para `viewer`
- [x] Manter modal completo para editar os demais campos

### Fase 10 — Alteração de Categoria em Lote
- [x] Criar query para atualizar categoria de múltiplas transações do board
- [x] Criar server action para aplicar categoria em lote
- [x] Adicionar checkbox por transação para usuários com permissão de mutação
- [x] Permitir selecionar transação clicando no card sem sobrepor inputs/botões
- [x] Adicionar controle de selecionar/limpar transações visíveis no painel
- [x] Exibir barra contextual apenas quando houver seleção
- [x] Usar input de categoria com sugestões existentes
- [x] Aplicar categoria, limpar seleção e revalidar dashboard
- [x] Esconder seleção múltipla para `viewer`

### Fase 11 — Imagem de Fechamento Mensal
- [x] Criar modal para ajustes temporários de fechamento
- [x] Receber aluguel, água e luz como saídas temporárias
- [x] Receber entrada extra opcional com categoria e valor
- [x] Receber saída extra opcional com categoria e valor
- [x] Agregar entradas e saídas por categoria
- [x] Gerar PNG client-side com cabeçalho, colunas e resumo final
- [x] Baixar imagem com nome legível
- [x] Não persistir valores temporários no DB
- [x] API: `GET /api/boards/{boardId}/periods` (início/fim inclusivos, saldo anterior = tudo antes do início, 400 para período inválido ou > 366 dias) com testes
- [x] Modal: campos de início e fim (padrão = mês selecionado), buscar resumo do período na API e gerar a imagem com ele
- [x] Imagem: subtítulo e nome do arquivo mostram o período quando não é o mês inteiro

### Fase 12 — Link Público View-Only
- [x] Criar tabela `board_share_links` com `token_hash`, `revoked_at` e `expires_at`
- [x] Criar client server-only para operação segura com service role
- [x] Criar server action para gerar/copiar link público
- [x] Criar rota pública `/share/[token]`
- [x] Validar token por hash e carregar board/transações do mês
- [x] Renderizar dashboard view-only sem ações de mutação
- [x] Esconder geração de imagem de fechamento na visualização pública
- [x] Retornar `notFound()` para token inválido, expirado ou revogado
- [x] Garantir que token cru nunca é salvo no banco

### Fase 13 — Reservas Financeiras
- [x] Criar tabela `financial_reserves` com RLS (`admin`/`tesoureiro` mutam, `viewer` e público apenas leem o total)
- [x] Criar query layer `financial-reserves.ts` (list/create/update/delete)
- [x] Criar server actions de reserva (criar/editar/remover)
- [x] Criar `FinancialReservesModal` com listagem, cadastro, edição e remoção (com confirmação)
- [x] Exibir botão "Reservas financeiras" no painel de Fechamento (apenas `admin`/`tesoureiro`)
- [x] Exibir total de reservas logo abaixo do saldo final no painel de Fechamento
- [x] Garantir que o total de reservas é apenas informativo e não altera o saldo final
- [x] Exibir total de reservas na imagem de fechamento mensal gerada
- [x] Exibir total de reservas também no link público view-only

### Fase 14 — Validação de Lançamento MVP
- [ ] Rodar `pnpm typecheck`
- [ ] Rodar `pnpm lint`
- [ ] Rodar `pnpm build`
- [ ] Testar usuário com board: login → `/board` → mês atual
- [ ] Testar criar, editar e remover transação no mês selecionado
- [ ] Testar edição rápida de descrição com confirmar/cancelar
- [ ] Testar alteração de categoria em lote com seleção múltipla
- [ ] Testar importar CSV Nubank com entradas e saídas
- [ ] Testar geração de imagem de fechamento com ajustes temporários
- [ ] Testar sugestões de categoria/descrição com histórico real
- [ ] Testar navegação mês anterior/próximo sem misturar totais
- [ ] Testar link público sem login e sem ações de mutação
- [ ] Testar token público revogado/inválido
- [ ] Testar mobile sem scroll global
- [ ] Testar cadastrar, editar e remover reservas financeiras (`admin`/`tesoureiro`)
- [ ] Testar que `viewer` e link público veem o total de reservas mas não o botão de gestão
- [ ] Testar que o total de reservas não altera o saldo final em nenhum cenário

### Fase 15 — Migração Supabase → ASP.NET Core + PostgreSQL ✅
Detalhes, arquivos afetados e critérios de cada etapa em [docs/refactor-plan.md](../docs/refactor-plan.md). Cada etapa deixa o projeto funcionando e é commitável isoladamente.
- [x] Etapa 0 — Spec atualizado para a arquitetura alvo
- [x] Etapa 1 — Limpeza de código morto (`next-themes`, `lib/supabase/admin.ts`, `client.ts`, `package-lock.json`)
- [x] Etapa 2 — Reestruturar em monorepo (`apps/web/src`), criar `CLAUDE.md` com `@AGENTS.md`
- [x] Etapa 3 — `docker-compose.dev.yml` (Postgres 18), esqueleto da API, schema e migration `Initial`, OpenAPI gerado no build
- [x] Etapa 4 — Login Google, allowlist em `users`, cookie de sessão, `GET /api/me`, rewrite do Next para `/api`
- [x] Etapa 5 — Endpoints de leitura, `BoardAccess`, resumo do mês calculado na API
- [x] Etapa 6 — Endpoints de escrita, import Nubank, link de compartilhamento
- [x] Etapa 7 — Cliente TypeScript gerado (`pnpm gen:api`), `api-client.ts`, `money.ts`
- [x] Etapa 8 — Front passa a consumir a API; login por senha, `forgot-password` e `reset-password` removidos
- [x] Etapa 9 — Remover Supabase (dependências, `lib/supabase`, `types/database.ts`, `supabase/schema.sql`, `.mcp.json`); README reescrito

### Fase 16 — Gestão de Boards
- [x] API: `CanManageBoards` (admin global), `GET /api/admin/boards`, `POST`, `PUT` e `DELETE` de board, `canManageBoards` em `/api/me`, com testes
- [x] Front: tela `/boards` (listar, criar, renomear, remover com confirmação pelo nome)
- [x] Front: troca de board e "Gerenciar boards" no menu (hoje na sidebar); `/board` sem boards leva o admin global a `/boards`

### Fase 17 — Tarefas (kanban) e papel `junta_administrativa`
- [x] Papel `junta_administrativa` e vários papéis por pessoa no mesmo board (migration em `user_roles`)
- [x] Tabelas `tasks` e `task_assignees`, endpoints, `CanManageTasks` e testes
- [x] Front: quadro kanban com arrastar, modal de tarefa, tags e responsáveis, visão somente leitura

### Fase 18 — Estoque
- [x] Tabela `stock_items`, endpoints, regra de preço (0 a 9.999.999.999,99) e testes
- [x] Front: tabela com busca, filtro por status, ordenação, status na linha e modal de item

### Fase 19 — Dashboard (tela inicial)
- [x] `GET /api/boards/{id}/dashboard` (caixa, histórico de 6 meses, tarefas, estoque) e testes
- [x] Front: cartões de indicador, gráfico de colunas, blocos de tarefas e estoque; dashboard como tela inicial

### Fase 20 — Prioridade nas tarefas
- [x] Coluna `tasks.priority` (opcional, check), API (criar/editar/listar), dashboard e testes
- [x] Front: selo no cartão, campo no modal, filtro e ordenação por prioridade, selo em "Minhas tarefas"

### Backlog Pós-MVP
- [ ] BoardSelector completo
- [ ] Página de administração de usuários e papéis (boards já têm tela, seção 7.12)
- [ ] Revogar e listar links de compartilhamento
- [ ] Tabela própria de categorias
- [ ] Deploy em VPS: Dockerfiles, `docker-compose.yml` de produção, Caddy (`/api` → API, resto → web), domínio, volume de chaves do Data Protection, backup do Postgres
- [ ] CI: `dotnet test`, lint e typecheck do web, checagem de `openapi.json` desatualizado
- [ ] Testes e2e do front no repositório (ex.: Playwright). Na Etapa 8 os fluxos foram validados com um roteiro descartável via Chrome DevTools Protocol, que não ficou no repo
- [ ] Controle avançado de visibilidade por role
- [ ] Dark/Light mode (toggle visível)
- [ ] Feedback de loading states
- [ ] Tratamento avançado de erros
- [ ] Testes automatizados abrangentes

---

## 13. Premissas e Perguntas Abertas

### Premissas do MVP
- Categoria não terá tabela própria no MVP; opções vêm de `distinct category` das transações do board
- Descrição também não terá tabela própria; sugestões vêm de `distinct description`
- `admin` e `tesoureiro` podem criar, editar e remover transações do board
- Link público view-only não expira por padrão, mas pode ser revogado
- Link público permite navegar por todos os meses do board
- Link público usa token opaco forte; banco salva apenas hash do token
- Fechamento mensal usa timezone operacional `America/Sao_Paulo`
- Editar categoria/descrição altera apenas a transação editada
- Reservas financeiras não têm filtro por mês; representam um valor acumulado do board
- Total de reservas financeiras é somente informativo e nunca entra no cálculo do saldo final
- Login com Google (e-mail verificado) ou com e-mail + senha (hash em `users.password_hash`, definido por comando); só e-mails cadastrados em `users` entram. Cadastro e papéis iniciais via seed SQL
- Papel global (`board_id` nulo) lê todos os boards; `admin` e `tesoureiro` globais também escrevem
- Sessão: cookie de 7 dias deslizantes, com `is_active` revalidado a cada request
- Front e API compartilham o mesmo origin. Em dev, o Next faz rewrite de `/api/*` para a API; no futuro, um reverse proxy
- Não há dados a migrar do Supabase: o banco novo começa vazio, populado só pelo seed

### Perguntas Abertas
- Nenhuma no momento.

---

## 14. Referências

- [Next.js App Router Docs](https://nextjs.org/docs/app)
- [ASP.NET Core](https://learn.microsoft.com/aspnet/core)
- [EF Core](https://learn.microsoft.com/ef/core) e [Npgsql EF Core Provider](https://www.npgsql.org/efcore)
- [Autenticação Google no ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/social/google-logins)
- [OpenAPI no ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
- [openapi-typescript](https://openapi-ts.dev)
- [shadcn/ui](https://ui.shadcn.com)
- [Tailwind CSS v4](https://tailwindcss.com/docs)
- [Conventional Commits](https://www.conventionalcommits.org)
