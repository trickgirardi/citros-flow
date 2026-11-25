# Spec — Estoque (tabela de itens)

Status: aprovado em 2026-11-26. Substitui a página "Em construção" de `/board/{id}/estoque`.

## Decisões

- **Tabela** de itens do board com as colunas: **Item**, **Status**, **Responsável**, **Preço** e **Detalhes**.
- **Status fixos:** `Em estoque`, `A caminho`, `Comprar`, `Comprar com urgência`.
- **Obrigatórios:** só **Item** e **Status**. Responsável, Preço e Detalhes são opcionais (na tabela, o que falta aparece como "—").
- **Responsável:** uma única pessoa, escolhida entre quem pode ser responsável no board (a mesma lista das tarefas: pessoas ativas com papel no board ou global).
- **Preço:** em reais, `numeric(12,2)`. É o preço do item; **não há quantidade** nem total (fora de escopo).
- **Recursos da tabela:** busca pelo nome do item; filtro por status com contagem; ordenar clicando no cabeçalho; trocar o status direto na linha (só quem gerencia).
- **Permissões:** as mesmas das tarefas. Qualquer papel no board **lê**; só `admin` e `junta_administrativa` (`CanManageTasks`) criam, editam, trocam o status e removem.
- O preço do estoque **não** se liga ao financeiro (não gera transação).

## Banco (migration)

`stock_items`: `id`, `board_id` (cascade), `name` (obrigatório, até 120), `status` (`em_estoque` | `a_caminho` | `comprar` | `comprar_com_urgencia`, check), `assignee_id` (opcional, FK `users`, `on delete set null`), `price` (`numeric(12,2)`, opcional, check `>= 0`), `details` (até 2000, padrão vazio), `created_by`/`created_at`, `updated_by`/`updated_at`. Índice `(board_id, status)`.

## API

Sem sessão 401; board que a pessoa não vê 404; sem permissão de escrita 403.

| Rota | O que faz |
|---|---|
| `GET /api/boards/{id}/stock-items` | itens do board (qualquer papel), por nome (pt-BR), com o responsável (`id`, `name`) ou nulo |
| `POST /api/boards/{id}/stock-items` | cria |
| `PUT /api/boards/{id}/stock-items/{itemId}` | edita todos os campos |
| `PATCH /api/boards/{id}/stock-items/{itemId}/status` | `{ status }`: troca só o status (usado na linha da tabela) |
| `DELETE /api/boards/{id}/stock-items/{itemId}` | remove |

- Corpo de criar/editar: `name`, `status`, `assigneeId`, `price` (string decimal, ex.: `"12.50"`), `details`.
- **Validação (400):** `name` obrigatório sem espaços nas pontas, até 120; `status` válido e obrigatório; `details` até 2000; `price` opcional, entre 0 e 9.999.999.999,99, arredondado para centavos (metade para longe do zero); `assigneeId`, se informado, tem de ser pessoa **ativa com acesso ao board** (senão 400).
- Candidatos a responsável: `GET /api/boards/{id}/task-assignees` (já existe; só para quem gerencia).
- Escritas gravam `updated_by`/`updated_at` da sessão; criação grava `created_by`.

## Tela `/board/{id}/estoque`

- Cabeçalho: título "Estoque"; ação **Novo item** só para quem gerencia.
- Barra acima da tabela: **busca** pelo nome (sem distinguir maiúsculas e acentos) e **filtros de status** (Todos, e cada status), todos com a contagem. Com busca e filtro sem resultado: "Nenhum item encontrado".
- **Tabela** em tela única: o cabeçalho fica fixo e as linhas rolam por dentro; a página não rola. Colunas Item, Status, Responsável, Preço (alinhado à direita, `R$ 1.234,56`) e Detalhes (uma linha, cortada). Pela largura do conteúdo (container query): abaixo de 56rem some Detalhes; abaixo de 42rem somem também Responsável e Preço, que passam para uma linha pequena sob o nome do item (e Detalhes aparece no modal).
- **Ordem padrão:** urgência primeiro (`Comprar com urgência`, `Comprar`, `A caminho`, `Em estoque`), depois por nome. Clicar no cabeçalho de Item, Status, Responsável ou Preço ordena (crescente; clicar de novo, decrescente; de novo, volta ao padrão). `aria-sort` no cabeçalho. Itens sem responsável ou sem preço ficam por último em qualquer sentido.
- **Status:** selo com texto e cor (nunca só cor). Quem gerencia troca por um seletor na própria linha, com efeito imediato (otimista; volta atrás com mensagem se a API recusar).
- **Modal de item** (clicar na linha ou **Novo item**): Item (obrigatório), Status (obrigatório; padrão `Comprar`), Responsável (lista com "Sem responsável"), Preço (aceita `1.234,56` e `12,5`), Detalhes. Editar tem **Remover** com confirmação. Quem só lê abre o modal em leitura.
- Vazio: "Nenhum item no estoque ainda." (e, para quem gerencia, o botão **Novo item**).

## Fora de escopo

Quantidade, unidade, categorias, histórico de preço, integração com o financeiro, total dos preços, importação de planilha.

## Validação

`dotnet test` (permissões por papel, validações, preço, responsável, status, cascata, constraints), `pnpm typecheck`, `pnpm lint`, `pnpm build`, e capturas de tela (admin e leitura, busca, filtro, ordenação, troca de status, modal, mobile, dois temas).

## Checklist

- [x] Migration, entidade, extração de `AssignableUsers`, endpoints e testes
- [x] OpenAPI e cliente TypeScript
- [x] Front: tabela, busca/filtro/ordenação, status na linha, modal, actions
- [x] Sidebar: Estoque deixa de ser "Em breve"
- [x] `core-spec.md` e `tarefas-spec.md`/README (referências)
- [x] Validação (testes, build e capturas de tela)
