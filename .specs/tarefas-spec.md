# Spec — Gestão de tarefas (kanban) e papel `junta_administrativa`

Status: aprovado em 2026-11-26. Substitui a página "Em construção" de `/board/{id}/tarefas`.

## Decisões

- **Colunas fixas:** `A fazer`, `Em andamento`, `Concluída`, iguais em todos os boards.
- **Tags:** texto livre por tarefa, com sugestões das tags já usadas no board. Sem tela de gestão de tags.
- **Arrastar:** `@dnd-kit` (dependência nova, autorizada) para mover entre colunas e reordenar, com mouse, toque e teclado. Cada cartão tem também o menu "Mover para" (alternativa acessível).
- **Permissões:** criar, editar, mover e remover tarefas só com `junta_administrativa` ou `admin`. **Quem tem qualquer papel no board vê o quadro**, em leitura. O financeiro não muda (escrever = `admin` ou `tesoureiro`). Uma pessoa só com `junta_administrativa` vê o fluxo de caixa em leitura, como um `viewer`.
- **Vários papéis por pessoa:** a mesma pessoa pode ter mais de um papel no mesmo board (ex.: `admin`, `tesoureiro` e `junta_administrativa`). As permissões se somam.
- O estoque usa a mesma regra das tarefas (`.specs/estoque-spec.md`).
- **Prioridade** opcional por tarefa: ver `.specs/tarefas-prioridade-spec.md`.

## Papéis

| Papel | Lê o board | Escreve no financeiro | Escreve em tarefas (e estoque) |
|---|---|---|---|
| `admin` | ✅ | ✅ | ✅ |
| `tesoureiro` | ✅ | ✅ | ❌ |
| `junta_administrativa` | ✅ | ❌ | ✅ |
| `viewer` | ✅ | ❌ | ❌ |

Vale no board ou global (`board_id` nulo). Regra de soma: o contexto do board expõe `CanMutate` (financeiro: `admin` ou `tesoureiro`) e `CanManageTasks` (`admin` ou `junta_administrativa`). Gestão de boards continua só do `admin` global.

## Banco (migration)

- `user_roles`: o check passa a aceitar `junta_administrativa`; a restrição única passa de `(user_id, board_id)` para **`(user_id, board_id, role)`** (`NULLS NOT DISTINCT`, para não duplicar o mesmo papel global). Linhas existentes continuam válidas.
- `tasks`: `id`, `board_id` (cascade), `name` (obrigatório, até 120), `description` (até 2000, padrão vazio), `status` (`a_fazer` | `em_andamento` | `concluida`, check), `position` (inteiro, ordem dentro da coluna), `tags` (`text[]`), `created_by`/`created_at`, `updated_by`/`updated_at`. Índice `(board_id, status, position)`.
- `task_assignees`: `(task_id, user_id)` chave composta, os dois com cascade.

## API

Autorização como no resto: sem sessão 401; board que a pessoa não vê 404; sem permissão de escrita 403.

| Rota | O que faz |
|---|---|
| `GET /api/boards/{id}/tasks` | todas as tarefas do board (qualquer papel), ordenadas por coluna e posição, com responsáveis (`id`, `name`) e tags |
| `GET /api/boards/{id}/task-assignees` | quem pode ser responsável: pessoas ativas com papel no board ou global (`id`, `name`). Só quem gerencia tarefas (403 para os outros) |
| `POST /api/boards/{id}/tasks` | cria; vai para o fim da coluna do `status` |
| `PUT /api/boards/{id}/tasks/{taskId}` | edita; se o `status` mudar, vai para o fim da nova coluna |
| `PATCH /api/boards/{id}/tasks/{taskId}/move` | `{ status, index }`: move para a coluna e posição (0 = topo; `index` além do fim vai para o fim) e renumera as colunas afetadas |
| `DELETE /api/boards/{id}/tasks/{taskId}` | remove (apaga os responsáveis em cascata) |

- Corpo de criar/editar: `name`, `description`, `status`, `tags[]`, `assigneeIds[]`.
- **Validação (400):** `name` obrigatório sem espaços nas pontas, até 120; `description` até 2000; `status` válido; até 10 tags de até 30 caracteres, sem vazias, repetidas (sem distinguir maiúsculas) removidas; até 10 responsáveis, todos pessoas **ativas com acesso ao board** (senão 400); `index` inteiro ≥ 0.
- `GET /api/boards/{id}` ganha `canManageTasks`.
- Cada escrita grava `updated_by`/`updated_at` (criação grava `created_by`).

## Tela `/board/{id}/tarefas`

- Cabeçalho: título "Gestão de tarefas"; ação **Nova tarefa** só para quem gerencia.
- Quadro de tela única: 3 colunas lado a lado a partir de 48rem de conteúdo (container query), cada uma com título, contador e rolagem **dentro da coluna**; abaixo disso as colunas rolam na horizontal. A página não rola.
- Cartão: nome, descrição (2 linhas), tags (chips) e responsáveis (iniciais). Quem gerencia: arrasta; clica para editar; menu "Mover para".
- Modal de tarefa: Nome (obrigatório), Descrição, Status (obrigatório), Responsáveis (lista com caixas de marcar), Tags (digita e Enter/vírgula cria o chip; sugestões das já usadas). Editar tem **Remover** com confirmação.
- Quem só lê vê o quadro sem arrastar, sem menu e sem "Nova tarefa".
- Arrastar é otimista (o cartão muda na hora) e volta atrás com mensagem se a API recusar.
- Vazio: cada coluna mostra "Nenhuma tarefa".

## Seed e docs

`infra/postgres/seed.example.sql` e o README passam a usar `on conflict (user_id, board_id, role)` e a citar `junta_administrativa` e os vários papéis.

## Fora de escopo

Data de entrega, comentários, anexos, colunas personalizadas, tela de gestão de tags, notificações, estoque.

## Validação

`dotnet test` (autorização por papel e combinações, validações, mover/reordenar, cascata, restrições do banco), `pnpm typecheck`, `pnpm lint`, `pnpm build`, e capturas de tela do quadro (admin e leitura, arrastar, modal, mobile, dois temas).

## Checklist

- [x] Migration (papéis múltiplos + `junta_administrativa`, `tasks`, `task_assignees`), entidades e `BoardAccess.CanManageTasks`
- [x] Endpoints de tarefas e de responsáveis, `canManageTasks` no board, testes
- [x] OpenAPI e cliente TypeScript
- [x] Front: dependência `@dnd-kit`, quadro, cartão, modal, ações
- [x] Sidebar: Tarefas deixa de ser "Em breve"
- [x] Seed, README e `core-spec.md` (papéis, UserRole, schema)
- [x] Validação (testes, build e capturas de tela)
