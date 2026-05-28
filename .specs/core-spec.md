# 🍋 SPEC — Citros Flow

---

## 1. Visão Geral

**Citros** é um sistema financeiro minimalista para fechamentos de caixa de instituições sem fins lucrativos. Prioriza visualização rápida e entrada ágil de dados via dashboard de tela única (single-screen no desktop, sem scroll global).

### Princípios Fundamentais
- **Clareza acima de tudo** — o usuário deve saber o saldo em menos de 2 segundos ao abrir o sistema
- **Zero fricção na entrada** — inserir uma transação deve exigir o mínimo de cliques possível
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
│   │       └── [boardId]/
│   │           └── page.tsx        # Dashboard principal (single-screen)
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
│   │       └── boards.ts
│   └── utils.ts                    # cn(), formatCurrency(), etc.
├── types/
│   └── database.ts                 # Tipos gerados pelo Supabase CLI
└── middleware.ts                   # Proteção de rotas via Supabase Auth
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
}
```

### 4.4 UserRole (Permissão)
Vincula usuário a um board com um nível de acesso.

```typescript
type UserRole = {
  id: string
  user_id: string      // FK → auth.users
  board_id: string     // FK → boards (null = acesso global para admin)
  role: 'admin' | 'tesoureiro' | 'viewer'
}
```

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
  created_at timestamptz default now()
);

-- user_roles
create table user_roles (
  id uuid primary key default gen_random_uuid(),
  user_id uuid references auth.users(id) on delete cascade not null,
  board_id uuid references boards(id) on delete cascade,
  role text check (role in ('admin', 'tesoureiro', 'viewer')) not null,
  unique(user_id, board_id)
);
```

---

## 6. Row Level Security (RLS)

### Política por Role

| Role | SELECT | INSERT | UPDATE | DELETE |
|---|---|---|---|---|
| admin | ✅ global | ✅ global | ✅ global | ✅ global |
| tesoureiro | ✅ boards vinculados | ✅ boards vinculados | ✅ boards vinculados | ❌ |
| viewer | ✅ board próprio | ❌ | ❌ | ❌ |

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

---

## 7. Interface e Layout

### 7.1 Dashboard (Tela Principal)

```
┌─────────────────────────────────────────────────────┐
│  Header: Logo | Board Selector | User Menu          │
├──────────────┬──────────────┬───────────────────────┤
│   ENTRADAS   │    SAÍDAS    │     FECHAMENTO        │
│  (scroll)    │  (scroll)    │     (estático)        │
│              │              │                       │
│ — Doações —  │ — Operac. — │  Total Entradas       │
│ [transação]  │ [transação]  │  Total Saídas         │
│ [transação]  │ [transação]  │  ─────────────        │
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

### 7.3 Modal de Transação
- Componente `Dialog` do shadcn/ui
- Campos: Tipo (entrada/saída), Valor, Descrição, Categoria, Data
- Validação client-side antes do submit
- Feedback visual de loading e erro

---

## 8. Autenticação

- Provider: Supabase Auth (email/password)
- Proteção de rotas via `middleware.ts`
- Sessão gerenciada pelo `@supabase/ssr`
- Redirect automático: não autenticado → `/login`

---

## 9. Variáveis de Ambiente

```bash
# .env.local
NEXT_PUBLIC_SUPABASE_URL=
NEXT_PUBLIC_SUPABASE_ANON_KEY=
SUPABASE_SERVICE_ROLE_KEY=     # apenas server-side
```

---

## 10. RBAC — Controle de Acesso

| Role | Descrição | Escopo |
|---|---|---|
| `admin` | CRUD total em Accounts, Boards e Usuários | Global |
| `tesoureiro` | Edita estrutura, insere e aprova transações | Boards vinculados |
| `viewer` | Apenas visualiza fechamento | Board específico |

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

### Fase 1 — Fundação
- [ ] Configurar Supabase (projeto, schema, RLS)
- [ ] Configurar variáveis de ambiente
- [ ] Implementar autenticação (login/logout)
- [ ] Middleware de proteção de rotas
- [ ] Tipos TypeScript exportados do Supabase

### Fase 2 — Dashboard Core
- [ ] Layout single-screen (100vh, 3 colunas)
- [ ] EntradasPanel com scroll isolado e agrupamento por categoria
- [ ] SaidasPanel com scroll isolado e agrupamento por categoria
- [ ] FechamentoPanel com totais e saldo

### Fase 3 — Transações
- [ ] TransactionModal (Dialog shadcn)
- [ ] Formulário com validação
- [ ] Insert no Supabase com RLS
- [ ] Atualização em tempo real dos painéis

### Fase 4 — RBAC e Admin
- [ ] BoardSelector no Header
- [ ] Controle de visibilidade por role
- [ ] Página de administração (CRUD de boards/usuários)

### Fase 5 — Refinamento
- [ ] Dark/Light mode
- [ ] Feedback de loading states
- [ ] Tratamento de erros
- [ ] Testes básicos

---

## 13. Referências

- [Next.js App Router Docs](https://nextjs.org/docs/app)
- [Supabase JS Client](https://supabase.com/docs/reference/javascript)
- [shadcn/ui](https://ui.shadcn.com)
- [Tailwind CSS v4](https://tailwindcss.com/docs)
- [Conventional Commits](https://www.conventionalcommits.org)