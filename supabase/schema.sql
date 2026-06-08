-- Citros Flow - Fase 1 (Fundacao)
-- Execute este arquivo no SQL Editor do Supabase.

create extension if not exists pgcrypto;

create table if not exists public.accounts (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  created_at timestamptz not null default now()
);

create table if not exists public.boards (
  id uuid primary key default gen_random_uuid(),
  account_id uuid not null references public.accounts(id) on delete cascade,
  name text not null,
  created_at timestamptz not null default now()
);

create table if not exists public.transactions (
  id uuid primary key default gen_random_uuid(),
  board_id uuid not null references public.boards(id) on delete cascade,
  type text not null check (type in ('entrada', 'saida')),
  amount numeric(12,2) not null check (amount > 0),
  description text not null,
  category text not null,
  date date not null,
  created_by uuid not null references auth.users(id),
  created_at timestamptz not null default now()
);

create table if not exists public.user_roles (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  board_id uuid references public.boards(id) on delete cascade,
  role text not null check (role in ('admin', 'tesoureiro', 'viewer')),
  unique (user_id, board_id)
);

create table if not exists public.board_share_links (
  id uuid primary key default gen_random_uuid(),
  board_id uuid not null references public.boards(id) on delete cascade,
  token_hash text not null unique,
  created_by uuid not null references auth.users(id) on delete cascade,
  created_at timestamptz not null default now(),
  revoked_at timestamptz,
  expires_at timestamptz
);

create index if not exists idx_user_roles_user_id on public.user_roles(user_id);
create index if not exists idx_user_roles_board_id on public.user_roles(board_id);
create index if not exists idx_transactions_board_id on public.transactions(board_id);
create index if not exists idx_transactions_created_by on public.transactions(created_by);
create index if not exists idx_boards_account_id on public.boards(account_id);
create index if not exists idx_board_share_links_board_id on public.board_share_links(board_id);
create index if not exists idx_board_share_links_created_by on public.board_share_links(created_by);
create index if not exists idx_board_share_links_token_hash on public.board_share_links(token_hash);

revoke all on public.accounts from anon, authenticated;
revoke all on public.boards from anon, authenticated;
revoke all on public.transactions from anon, authenticated;
revoke all on public.user_roles from anon, authenticated;
revoke all on public.board_share_links from anon, authenticated;

grant usage on schema public to authenticated;
grant select on public.accounts to authenticated;
grant select, insert, update on public.boards to authenticated;
grant select, insert, update, delete on public.transactions to authenticated;
grant select on public.user_roles to authenticated;
grant select, insert, update on public.board_share_links to authenticated;

alter table public.accounts enable row level security;
alter table public.boards enable row level security;
alter table public.transactions enable row level security;
alter table public.user_roles enable row level security;
alter table public.board_share_links enable row level security;

-- Remove politicas antigas com mesmo nome para permitir reexecucao.
drop policy if exists "accounts_select_by_membership" on public.accounts;
drop policy if exists "boards_select_by_membership" on public.boards;
drop policy if exists "boards_insert_for_admin_or_tesoureiro" on public.boards;
drop policy if exists "boards_update_for_admin_or_tesoureiro" on public.boards;
drop policy if exists "transactions_select_by_membership" on public.transactions;
drop policy if exists "transactions_insert_for_admin_or_tesoureiro" on public.transactions;
drop policy if exists "transactions_update_for_admin_or_tesoureiro" on public.transactions;
drop policy if exists "transactions_delete_admin_only" on public.transactions;
drop policy if exists "transactions_delete_for_admin_or_tesoureiro" on public.transactions;
drop policy if exists "user_roles_select_own" on public.user_roles;
drop policy if exists "board_share_links_select_by_board_membership" on public.board_share_links;
drop policy if exists "board_share_links_insert_for_admin_or_tesoureiro" on public.board_share_links;
drop policy if exists "board_share_links_update_for_admin_or_tesoureiro" on public.board_share_links;

-- user_roles: cada usuario ve seus vinculos.
create policy "user_roles_select_own"
  on public.user_roles
  for select
  using ((select auth.uid()) = user_id);

-- boards: usuarios veem boards vinculados (ou globais admin).
create policy "boards_select_by_membership"
  on public.boards
  for select
  using (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and (
          ur.board_id = boards.id
          or (ur.role = 'admin' and ur.board_id is null)
        )
    )
  );

create policy "boards_insert_for_admin_or_tesoureiro"
  on public.boards
  for insert
  with check (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id is null or ur.board_id = boards.id)
    )
  );

create policy "boards_update_for_admin_or_tesoureiro"
  on public.boards
  for update
  using (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id is null or ur.board_id = boards.id)
    )
  )
  with check (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id is null or ur.board_id = boards.id)
    )
  );

-- accounts: acesso por associacao via board + user_roles.
create policy "accounts_select_by_membership"
  on public.accounts
  for select
  using (
    exists (
      select 1
      from public.boards b
      join public.user_roles ur on ur.board_id = b.id
      where b.account_id = accounts.id
        and ur.user_id = (select auth.uid())
    )
    or exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role = 'admin'
        and ur.board_id is null
    )
  );

-- transactions: leitura por board vinculado.
create policy "transactions_select_by_membership"
  on public.transactions
  for select
  using (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and (
          ur.board_id = transactions.board_id
          or (ur.role = 'admin' and ur.board_id is null)
        )
    )
  );

create policy "transactions_insert_for_admin_or_tesoureiro"
  on public.transactions
  for insert
  with check (
    created_by = (select auth.uid())
    and exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id is null or ur.board_id = transactions.board_id)
    )
  );

create policy "transactions_update_for_admin_or_tesoureiro"
  on public.transactions
  for update
  using (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id is null or ur.board_id = transactions.board_id)
    )
  )
  with check (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id is null or ur.board_id = transactions.board_id)
    )
  );

create policy "transactions_delete_for_admin_or_tesoureiro"
  on public.transactions
  for delete
  using (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id = transactions.board_id or ur.board_id is null)
    )
  );

create policy "board_share_links_select_by_board_membership"
  on public.board_share_links
  for select
  using (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and (
          ur.board_id = board_share_links.board_id
          or (ur.role = 'admin' and ur.board_id is null)
        )
    )
  );

create policy "board_share_links_insert_for_admin_or_tesoureiro"
  on public.board_share_links
  for insert
  with check (
    created_by = (select auth.uid())
    and exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id = board_share_links.board_id or ur.board_id is null)
    )
  );

create policy "board_share_links_update_for_admin_or_tesoureiro"
  on public.board_share_links
  for update
  using (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id = board_share_links.board_id or ur.board_id is null)
    )
  )
  with check (
    exists (
      select 1
      from public.user_roles ur
      where ur.user_id = (select auth.uid())
        and ur.role in ('admin', 'tesoureiro')
        and (ur.board_id = board_share_links.board_id or ur.board_id is null)
    )
  );
