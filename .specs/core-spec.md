# 🍋 SPEC — Citros Flow

---

## 1. Visão Geral

**Citros** é um sistema financeiro minimalista para fechamentos de caixa de instituições sem fins lucrativos. Prioriza visualização rápida e entrada ágil de dados via dashboard de tela única (single-screen no desktop, sem scroll global).

### Princípios Fundamentais
- **Clareza acima de tudo** — o usuário deve saber o saldo em menos de 2 segundos ao abrir o sistema
- **Zero fricção na entrada** — inserir uma transação deve exigir o mínimo de cliques possível
- **Fechamento mensal** — toda leitura financeira operacional acontece dentro de um mês/ano selecionado
- **Dados confiáveis** — RLS no banco garante que cada usuário vê apenas o que deve ver
- **IA-first, review-based** — código gerado por IA, revisado e aprovado por humano antes de merge

---

## 2. Stack Tecnológica

| Camada | Tecnologia | Versão |
|---|---|---|
| Framework | Next.js (App Router) | 16.x |
| Linguagem | TypeScript | 5.x |
| Runtime | Node.js | 24 LTS |
| Package Manager | pnpm | 11.x |
| Banco de Dados | Supabase (PostgreSQL) | — |
| Autenticação | Supabase Auth | — |
| UI Components | shadcn/ui + Radix UI | latest | 
| Estilização | Tailwind CSS | 4.x |
| Temas | next-themes | 0.4.x |
| Linting/Format | Prettier + ESLint | — |
| Deploy | Vercel | — |

---

## 3. Arquitetura do Projeto

### 3.1 Estrutura de Pastas

```
citros-flow/
├── app/
│   ├── (auth)/
│   │   └── login/
│   │       └── page.tsx
│   ├── (app)/
│   │   ├── layout.tsx              # Layout protegido (requer auth)
│   │   └── board/
│   │       ├── page.tsx            # Redirect para primeiro board ou empty state
│   │       └── [boardId]/
│   │           ├── actions.ts      # Server Actions do dashboard
│   │           └── page.tsx        # Dashboard principal (single-screen)
│   ├── share/
│   │   └── [token]/
│   │       └── page.tsx            # Visualização pública view-only
│   ├── api/
│   │   └── [...]/                  # Route handlers (se necessário)
│   ├── layout.tsx                  # Root layout
│   └── page.tsx                    # Redirect → login ou board
├── components/
│   ├── ui/                         # shadcn/ui (gerado automaticamente)
│   ├── panels/
│   │   ├── EntradasPanel.tsx
│   │   ├── SaidasPanel.tsx
│   │   └── FechamentoPanel.tsx
│   ├── modals/
│   │   └── TransactionModal.tsx
│   ├── board/
│   │   ├── MonthNavigator.tsx
│   │   ├── TransactionActions.tsx
│   │   └── ShareBoardButton.tsx
│   └── layout/
│       ├── Header.tsx
│       └── BoardSelector.tsx
├── hooks/
│   ├── useTransactions.ts
│   ├── useBoard.ts
│   └── useAuth.ts
├── lib/
│   ├── supabase/
│   │   ├── client.ts               # Cliente browser
│   │   ├── server.ts               # Cliente server (SSR/RSC)
│   │   └── queries/                # Todo acesso ao banco fica aqui
│   │       ├── transactions.ts
│   │       ├── boards.ts
│   │       └── share-links.ts
│   └── utils.ts                    # cn(), formatCurrency(), etc.
├── types/
│   └── database.ts                 # Tipos gerados pelo Supabase CLI
└── proxy.ts                        # Proteção de rotas via Supabase Auth (Next.js 16)
```

### 3.2 Fluxo de Dados

```
Supabase DB (PostgreSQL + RLS)
        ↓
Supabase JS Client (lib/supabase/)
        ↓
Server Components / Route Handlers (app/)
        ↓
Client Components (components/)
        ↓
UI (shadcn/ui + Tailwind)
```

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
  amount: number       // decimal(12,2)
  description: string
  category: string     // ex: "Doações", "Custos Operacionais"
  date: string         // date (YYYY-MM-DD)
  created_at: string
  created_by: string   // FK → auth.users
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

### 4.5 UserRole (Permissão)
Vincula usuário a um board com um nível de acesso.

```typescript
type UserRole = {
  id: string
  user_id: string      // FK → auth.users
  board_id: string     // FK → boards (null = acesso global para admin)
  role: 'admin' | 'tesoureiro' | 'viewer'
}
```

### 4.6 BoardShareLink (Link Público View-Only)
Permite visualizar um board sem login, apenas leitura.

```typescript
type BoardShareLink = {
  id: string
  board_id: string       // FK → boards
  token_hash: string     // hash do token público; token cru não fica salvo
  created_by: string     // FK → auth.users
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

---

## 5. Schema SQL (Supabase)

```sql
-- accounts
create table accounts (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  created_at timestamptz default now()
);

-- boards
create table boards (
  id uuid primary key default gen_random_uuid(),
  account_id uuid references accounts(id) on delete cascade not null,
  name text not null,
  created_at timestamptz default now()
);

-- transactions
create table transactions (
  id uuid primary key default gen_random_uuid(),
  board_id uuid references boards(id) on delete cascade not null,
  type text check (type in ('entrada', 'saida')) not null,
  amount numeric(12,2) not null check (amount > 0),
  description text not null,
  category text not null,
  date date not null,
  created_by uuid references auth.users(id) not null,
  created_at timestamptz default now(),
  updated_by uuid references auth.users(id),
  updated_at timestamptz
);

-- user_roles
create table user_roles (
  id uuid primary key default gen_random_uuid(),
  user_id uuid references auth.users(id) on delete cascade not null,
  board_id uuid references boards(id) on delete cascade,
  role text check (role in ('admin', 'tesoureiro', 'viewer')) not null,
  unique(user_id, board_id)
);

-- board_share_links
create table board_share_links (
  id uuid primary key default gen_random_uuid(),
  board_id uuid references boards(id) on delete cascade not null,
  token_hash text not null unique,
  created_by uuid references auth.users(id) not null,
  created_at timestamptz default now(),
  revoked_at timestamptz,
  expires_at timestamptz
);

-- indices essenciais do MVP
create index transactions_board_date_idx on transactions(board_id, date desc);
create index transactions_board_category_idx on transactions(board_id, category);
create index transactions_board_description_idx on transactions(board_id, description);
create index board_share_links_board_id_idx on board_share_links(board_id);
create index board_share_links_active_idx on board_share_links(token_hash)
  where revoked_at is null;
```

---

## 6. Row Level Security (RLS)

### Política por Role

| Role | SELECT | INSERT | UPDATE | DELETE |
|---|---|---|---|---|
| admin | ✅ global | ✅ global | ✅ global | ✅ global |
| tesoureiro | ✅ boards vinculados | ✅ boards vinculados | ✅ boards vinculados | ✅ boards vinculados |
| viewer | ✅ board próprio | ❌ | ❌ | ❌ |
| público view-only | ✅ apenas via `/share/[token]` | ❌ | ❌ | ❌ |

```sql
-- Habilitar RLS
alter table transactions enable row level security;
alter table boards enable row level security;

-- Exemplo: política de leitura de transactions
create policy "users can view their board transactions"
  on transactions for select
  using (
    board_id in (
      select board_id from user_roles
      where user_id = auth.uid()
    )
  );

-- Exemplo: política de insert de transactions
create policy "usuarios can insert transactions"
  on transactions for insert
  with check (
    board_id in (
      select board_id from user_roles
      where user_id = auth.uid()
      and role in ('admin', 'tesoureiro')
    )
    and created_by = auth.uid()
  );
```

### Link público view-only
- `board_share_links` não deve ter `SELECT` liberado para `anon`
- Token público deve ser validado apenas no servidor, usando hash
- A rota `/share/[token]` usa acesso server-side controlado para buscar somente:
  - board público validado pelo token
  - transações do mês selecionado
  - totais de entradas, saídas e saldo
- Nenhuma Server Action de mutação deve existir na rota pública

---

## 7. Interface e Layout

### 7.1 Dashboard (Tela Principal)

```
┌─────────────────────────────────────────────────────┐
│ Header: Logo | Board | Mês/Ano | Share | User/Menu  │
├──────────────┬──────────────┬───────────────────────┤
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
- Header concentra contexto e comandos globais: usuário, board selecionado, mês/ano, link público e logout
- Área principal contém apenas informações e ações do board atual
- Sem scroll global no dashboard autenticado; scroll fica isolado nos painéis

### 7.3 Escopo Mensal
- Componente `MonthNavigator` fica no Header
- Controles mínimos: mês anterior, mês seguinte, mês atual
- URL carrega período selecionado via query string: `/board/[boardId]?month=YYYY-MM`
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
- Remoção e edição respeitam RLS e role do usuário
- Viewer não vê ações de mutação

### 7.8 Link Público View-Only
- Header mostra comando para gerar/copiar link público do board atual
- Link abre `/share/[token]`
- Página pública reaproveita visualização do dashboard, mas sem:
  - user menu
  - botão nova transação
  - editar/remover
  - board selector
- Página pública mantém navegação mensal simples
- Se token inválido, expirado ou revogado: renderizar `notFound()`

---

## 8. Autenticação

- Provider: Supabase Auth (email/password)
- Proteção de rotas via `proxy.ts` (Next.js 16; `middleware.ts` foi substituído)
- Sessão gerenciada pelo `@supabase/ssr`
- Redirect automático: não autenticado → `/login`

---

## 9. Variáveis de Ambiente

```bash
# .env.local
NEXT_PUBLIC_SUPABASE_URL=
NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY=
SUPABASE_SERVICE_ROLE_KEY=     # apenas server-side
```

---

## 10. RBAC — Controle de Acesso

| Role | Descrição | Escopo |
|---|---|---|
| `admin` | CRUD total em Accounts, Boards e Usuários | Global |
| `tesoureiro` | Cria, edita e remove transações | Boards vinculados |
| `viewer` | Apenas visualiza fechamento | Board específico |

Para o MVP, `admin` e `tesoureiro` têm as mesmas permissões sobre transações do board. Gestão avançada de usuários/boards fica fora do lançamento.

---

## 11. Convenções de Desenvolvimento

### Fluxo IA-First / Review-Based
1. **Prompt** → IA gera o código (feature completa ou componente)
2. **Review** → desenvolvedor revisa linha a linha
3. **Ajuste** → correções manuais ou novo prompt refinado
4. **Commit** → apenas após aprovação humana

### Acesso ao Banco de Dados

**Regra:** todo acesso ao Supabase vive exclusivamente em `lib/supabase/queries/`. Componentes e hooks **nunca** importam o cliente Supabase diretamente.

```typescript
// ✅ correto — lib/supabase/queries/transactions.ts
export async function getTransactions(boardId: string): Promise<Transaction[]> {
  const { data } = await supabase
    .from('transactions')
    .select('*')
    .eq('board_id', boardId)
  return data ?? []
}

// ❌ errado — chamar supabase direto no componente ou hook
const { data } = await supabase.from('transactions').select('*')
```

Essa convenção evita lock-in sem adicionar camadas de abstração desnecessárias. Se a infraestrutura mudar, apenas os arquivos em `queries/` precisam ser alterados.

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
- [ ] Criar/ajustar `Header` no layout protegido
- [ ] Mover board atual/selector simples para Header
- [ ] Mover navegação mês/ano para Header
- [ ] Exibir usuário atual e logout no Header
- [ ] Exibir gerar/copiar link público no Header
- [ ] Remover contexto duplicado da área principal
- [ ] Deixar main apenas com painéis e ações do board

### Fase 8 — Link Público View-Only
- [ ] Criar tabela `board_share_links` com `token_hash`, `revoked_at` e `expires_at`
- [ ] Criar client server-only para operação segura com service role
- [ ] Criar server action para gerar/copiar link público
- [ ] Criar rota pública `/share/[token]`
- [ ] Validar token por hash e carregar board/transações do mês
- [ ] Renderizar dashboard view-only sem ações de mutação
- [ ] Retornar `notFound()` para token inválido, expirado ou revogado
- [ ] Garantir que token cru nunca é salvo no banco

### Fase 9 — Validação de Lançamento MVP
- [ ] Rodar `pnpm typecheck`
- [ ] Rodar `pnpm lint`
- [ ] Rodar `pnpm build`
- [ ] Rodar Supabase advisors após migrações
- [ ] Testar usuário com board: login → `/board` → mês atual
- [ ] Testar criar, editar e remover transação no mês selecionado
- [ ] Testar sugestões de categoria/descrição com histórico real
- [ ] Testar navegação mês anterior/próximo sem misturar totais
- [ ] Testar link público sem login e sem ações de mutação
- [ ] Testar token público revogado/inválido
- [ ] Testar mobile sem scroll global

### Backlog Pós-MVP
- [ ] BoardSelector completo
- [ ] Página de administração (CRUD de boards/usuários)
- [ ] Controle avançado de visibilidade por role
- [ ] Dark/Light mode
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

### Perguntas Abertas
- Nenhuma no momento.

---

## 14. Referências

- [Next.js App Router Docs](https://nextjs.org/docs/app)
- [Supabase JS Client](https://supabase.com/docs/reference/javascript)
- [shadcn/ui](https://ui.shadcn.com)
- [Tailwind CSS v4](https://tailwindcss.com/docs)
- [Conventional Commits](https://www.conventionalcommits.org)
