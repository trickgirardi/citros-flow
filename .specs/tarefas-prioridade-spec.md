# Spec — Prioridade nas tarefas

Status: aprovado em 2026-11-25. Estende `.specs/tarefas-spec.md` (quadro kanban) e o bloco "Tarefas" de `.specs/dashboard-spec.md`.

## Decisões

- **Níveis fixos**, iguais em todos os boards: `Baixa`, `Média`, `Alta`, `Urgente`.
- **Opcional.** A tarefa pode ficar **sem prioridade** (tarefas existentes ficam assim; nada muda para elas).
- **Permissões:** as das tarefas. Qualquer papel no board vê a prioridade; só `admin` e `junta_administrativa` a definem (como o resto da tarefa).
- **Selo** com texto e cor (nunca só cor), com um ícone de bandeira para não se confundir com as tags. Sem prioridade, não aparece selo.
- **Ordenar por prioridade** é uma visão, não muda a ordem salva: um botão liga/desliga (padrão **desligado**, ordem manual). Ligado, cada coluna mostra Urgente, Alta, Média, Baixa e, por último, sem prioridade; empate pela ordem manual. Com ele ligado, **reordenar dentro da coluna não faz nada** (a ordem mostrada não é a salva); mover para outra coluna continua, indo para o fim dela.
- **Filtro por prioridade** é uma visão: Todas, cada nível e "Sem prioridade", com contagem. O filtro e a ordenação são do navegador e voltam ao padrão ao recarregar. Arrastar com filtro ligado vale pela ordem salva (a tarefa assume o lugar do cartão sobre o qual foi solta).
- **Dashboard:** "Minhas tarefas" mostra o selo e passa a listar **as mais prioritárias primeiro** (Urgente, Alta, Média, Baixa, sem prioridade); empate: em andamento antes de a fazer, depois a ordem do quadro.

## Banco (migration)

`tasks.priority`: `text`, **nulo**, check `priority is null or priority in ('baixa', 'media', 'alta', 'urgente')`. Sem índice (poucas tarefas por board).

## API

- `BoardTaskDto` ganha `priority` (`baixa` | `media` | `alta` | `urgente`, ou `null`).
- Corpo de criar/editar ganha `priority` (opcional). Como o `PUT` troca todos os campos, **omitir ou mandar `null` tira a prioridade**. Valor fora da lista: 400 (como o `status`).
- `PATCH .../move` não muda.
- Dashboard: `DashboardTaskItem` ganha `priority` (nulo se não tem); `mine` com a nova ordem.

## Tela `/board/{id}/tarefas`

- **Cartão:** selo da prioridade acima do nome.
- **Modal:** campo **Prioridade** (lista: "Sem prioridade", Baixa, Média, Alta, Urgente), entre Status e Responsáveis. Padrão "Sem prioridade". Em leitura, desabilitado.
- **Barra acima do quadro:** filtros de prioridade (com contagem) e o botão **Ordenar por prioridade** (`aria-pressed`). Com filtro, cada coluna mostra "2 de 5"; coluna sem resultado diz "Nenhuma tarefa".
- Tela única, como antes: a barra ocupa uma linha; as colunas continuam rolando por dentro.

## Fora de escopo

Níveis personalizados, prioridade por cor escolhida, prazo/data de entrega, salvar filtro ou ordenação, prioridade no estoque.

## Validação

`dotnet test` (criar/editar com e sem prioridade, valor inválido, `PUT` sem prioridade limpa, constraint do banco, ordem de "Minhas tarefas"), `pnpm typecheck`, `pnpm lint`, `pnpm build` e capturas de tela (cartão, modal, filtro, ordenação, dashboard, dois temas, mobile).

## Checklist

- [x] Migration, entidade, DTO, validação e endpoints, com testes
- [x] Dashboard: `priority` e nova ordem de "Minhas tarefas", com testes
- [x] OpenAPI e cliente TypeScript
- [x] Front: selo, campo no modal, filtro e ordenação, selo no dashboard
- [x] `core-spec.md`, `tarefas-spec.md` e `dashboard-spec.md`
- [x] Validação (testes, build e capturas de tela)
