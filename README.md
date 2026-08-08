# Citros Flow

**Citros** é um sistema financeiro minimalista para fechamentos de caixa de instituições sem fins lucrativos. Prioriza visualização rápida e entrada ágil de dados via dashboard de tela única (single-screen no desktop, sem scroll global).

## Princípios Fundamentais
- **Clareza acima de tudo** — o usuário deve saber o saldo em menos de 2 segundos ao abrir o sistema
- **Zero fricção na entrada** — inserir uma transação deve exigir o mínimo de cliques possível
- **Fechamento mensal** — toda leitura financeira operacional acontece dentro de um mês/ano selecionado
- **Dados confiáveis** — RLS no banco garante que cada usuário vê apenas o que deve ver
- **IA-first, review-based** — código gerado por IA, revisado e aprovado por humano antes de merge

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
