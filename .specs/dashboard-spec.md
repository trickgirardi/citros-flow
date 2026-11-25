# Spec — Dashboard (tela inicial do board)

Status: aprovado em 2026-11-26. Referência: tela inicial do `igb-sistema` (`/inicio`): cartões de indicador, gráfico de colunas e listas com link para a tela de cada assunto. Substitui a página "Em construção" de `/board/{id}/dashboard`.

## Decisões

- **Por board.** O dashboard é do board escolhido na sidebar. Qualquer papel no board lê (nada no dashboard escreve).
- **Blocos:** (1) Caixa do mês + gráfico de 6 meses, (2) Tarefas, (3) Estoque. Fora: maiores categorias de saída (fica para depois).
- **Tela inicial:** o login, `/board` e a troca de board fora de um módulo levam ao dashboard. `/board/{id}?month=...` (endereço antigo do fluxo de caixa) continua levando ao fluxo de caixa; `/board/{id}` sem mês vai ao dashboard.
- **Mês atual** (fuso `America/Sao_Paulo`), sem seletor de mês. O gráfico mostra os 6 meses que terminam nele.
- **Sem biblioteca de gráfico:** colunas em CSS, com tabela equivalente para leitor de tela (como o IGB).
- **Totais na API:** o front só formata. Uma única chamada traz tudo.

## API: `GET /api/boards/{boardId}/dashboard?month=yyyy-MM`

Qualquer papel no board (404 se não vê; 400 se `month` inválido). Resposta:

| Campo | Conteúdo |
|---|---|
| `month` | o mês pedido |
| `cash` | `previousBalance`, `entradasTotal`, `saidasTotal`, `balance`, `reservesTotal`, `transactionCount`: **os mesmos números do resumo mensal** (`/months/{month}`), sem as listas |
| `history` | 6 itens em ordem cronológica, o último é `month`: `{ month, entradas, saidas }`; mês sem lançamentos vem com `0.00` |
| `tasks` | `aFazer`, `emAndamento`, `concluida` (contagens); `mine`: até 5 tarefas **da pessoa logada** que não estão concluídas (`id`, `name`, `status`), em andamento primeiro; `mineTotal`: quantas são |
| `stock` | `emEstoque`, `aCaminho`, `comprar`, `comprarComUrgencia` (contagens); `attention`: até 5 itens para agir (`id`, `name`, `status`, `assignee`), `Comprar com urgência` primeiro, depois `Comprar`, por nome; `attentionTotal`: quantos são |

## Tela `/board/{id}/dashboard`

- Cabeçalho: título "Dashboard" e o nome do board acima. Abaixo, uma frase: "Resumo de novembro de 2026."
- **Caixa do mês:** título com o mês e link "Ver fluxo de caixa". Quatro **cartões de indicador** (número grande, rótulo e apoio): **Saldo final** (azul se positivo, vermelho se negativo; apoio "Saldo anterior R$ …"), **Entradas** (apoio: nº de transações), **Saídas**, **Reservas** ("informativo, fora do saldo"). Abaixo, o **gráfico de colunas** "Entradas e saídas nos últimos 6 meses": duas colunas por mês (entradas em azul, saídas em vermelho), eixo com o maior valor, rótulos curtos (`nov.`), mês atual em destaque, legenda e tabela equivalente.
- **Tarefas:** link "Ver tarefas"; três contagens (A fazer, Em andamento, Concluída) e a lista **Minhas tarefas** (nome + status; "Você ainda não tem tarefas em aberto." se vazia; "e mais N" se passar de 5). Cada tarefa leva a `/tarefas`.
- **Estoque:** link "Ver estoque"; quatro contagens por status e a lista **Precisa de ação** (nome, status e responsável; "Nada para comprar agora." se vazia; "e mais N").
- Colunas pela largura do **conteúdo** (container query): cartões em 2 colunas a partir de 28rem e em 4 a partir de 64rem (o número do saldo nunca quebra); Tarefas e Estoque lado a lado a partir de 56rem. A página rola dentro da área de conteúdo (a tela não).
- Estado vazio: board novo mostra zeros, o gráfico com meses zerados e as listas vazias, sem erro.

## Arquivos

API: `Features/Dashboard/DashboardEndpoints.cs`, `DashboardDtos.cs`. Web: `components/dashboard/` (`StatCard`, `CashChart`, `DashboardSection`, `TaskSummary`, `StockSummary`) e `board/[boardId]/dashboard/page.tsx`, tudo em Server Components (sem JavaScript no navegador).

## Fora de escopo

Seletor de mês, maiores categorias de saída, dashboard de todos os boards juntos, atualização automática, exportação.

## Validação

`dotnet test` (permissões, igualdade com o resumo mensal, 6 meses com virada de ano, contagens, "Minhas tarefas", "Precisa de ação", board vazio), `pnpm typecheck`, `pnpm lint`, `pnpm build` e capturas de tela (dois temas, 1440, ~1100 e celular).

## Checklist

- [x] API: endpoint e testes
- [x] OpenAPI e cliente TypeScript
- [x] Front: cartões, gráfico, blocos de tarefas e estoque, página
- [x] Tela inicial: `/board`, seletor de board e redirect antigo
- [x] `core-spec.md` e validação (testes, build, capturas)
