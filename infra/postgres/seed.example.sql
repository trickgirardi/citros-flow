-- Seed de exemplo: contas, boards, usuários liberados para login e seus papéis.
--
-- Copie para infra/postgres/seed.sql (ignorado pelo git), troque os dados de exemplo pelos reais
-- e rode DEPOIS das migrations. É idempotente: pode rodar de novo para adicionar ou ajustar gente.
--
-- Não existe auto-cadastro: só entra no sistema quem estiver em `users` e `is_active = true`.
-- Escreva os e-mails em MINÚSCULAS e sem espaços: o banco recusa e-mail fora desse formato.
--
-- Dev:
--   docker compose -f docker-compose.dev.yml exec -T postgres \
--     psql -U citros -d citros -v ON_ERROR_STOP=1 < infra/postgres/seed.sql
-- Produção:
--   psql "<connection string>" -v ON_ERROR_STOP=1 -f infra/postgres/seed.sql

begin;

-- Contas e boards: IDs fixos para o script poder ser reexecutado.
insert into accounts (id, name) values
  ('00000000-0000-0000-0000-0000000000a1', 'Sede Central')
on conflict (id) do update set name = excluded.name;

insert into boards (id, account_id, name) values
  ('00000000-0000-0000-0000-0000000000b1', '00000000-0000-0000-0000-0000000000a1', 'Departamento de Eventos'),
  ('00000000-0000-0000-0000-0000000000b2', '00000000-0000-0000-0000-0000000000a1', 'Departamento Social')
on conflict (id) do update set name = excluded.name, account_id = excluded.account_id;

-- Usuários liberados. Para desativar alguém: update users set is_active = false where email = '...';
insert into users (email, display_name) values
  ('admin@exemplo.org',      'Administrador'),
  ('tesoureiro@exemplo.org', 'Tesoureiro de Eventos'),
  ('leitor@exemplo.org',     'Leitor Social')
on conflict (email) do update set display_name = excluded.display_name;

-- Papéis: board_id nulo = papel global (vale para todos os boards).
-- Papéis: admin | tesoureiro | viewer.
insert into user_roles (user_id, board_id, role)
select id, null, 'admin' from users where email = 'admin@exemplo.org'
on conflict (user_id, board_id) do update set role = excluded.role;

insert into user_roles (user_id, board_id, role)
select id, '00000000-0000-0000-0000-0000000000b1', 'tesoureiro' from users where email = 'tesoureiro@exemplo.org'
on conflict (user_id, board_id) do update set role = excluded.role;

insert into user_roles (user_id, board_id, role)
select id, '00000000-0000-0000-0000-0000000000b2', 'viewer' from users where email = 'leitor@exemplo.org'
on conflict (user_id, board_id) do update set role = excluded.role;

commit;
