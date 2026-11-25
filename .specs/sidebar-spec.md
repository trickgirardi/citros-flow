# Spec — Sidebar lateral e módulos do board

Status: aprovado em 2026-11-25. Referência: `AppShell`/`SideMenu` do IGB (`igb-sistema/frontend/apps/web/src/components/layout`). Substitui o menu hambúrguer do cabeçalho.

## Decisões

- **Rotas:** os módulos ficam sob o board. O endereço antigo `/board/{id}?month=...` continua funcionando: redireciona para `/fluxo-de-caixa` mantendo o mês.
- **Abrir/fechar:** aberta por padrão no desktop; um botão no cabeçalho fecha por completo (o board ocupa a largura toda) e reabre. A escolha fica no cookie `sidebar` (`open`/`closed`), lido no servidor: sem piscar ao carregar. No celular (< 1024px) o mesmo botão abre uma **gaveta** por cima do conteúdo; sem barra inferior.
- **Módulos futuros:** Dashboard, Tarefas e Estoque aparecem no menu com selo "Em breve" e abrem uma página simples "Em construção" dentro do shell.
- **Troca de board:** seletor no topo da sidebar (dropdown). Trocar de board mantém o módulo atual.
- Os quatro módulos são **por board**. Se o Dashboard virar uma visão de todos os boards, muda só a rota dele.

## Rotas

| Rota | O que é |
|---|---|
| `/board` | vai ao primeiro board (ou `/boards` para admin global sem boards), como hoje |
| `/board/{id}` | redireciona para `/board/{id}/fluxo-de-caixa`, preservando `?month=` |
| `/board/{id}/fluxo-de-caixa` | o fluxo de caixa de hoje (single-screen) |
| `/board/{id}/dashboard`, `/tarefas`, `/estoque` | "Em construção" |
| `/boards` | gestão de boards (admin global), agora dentro do shell |

`/login` e `/share/{token}` ficam fora do shell.

## Sidebar (como no IGB, com a densidade do Citros)

- Largura `17.5rem`, fundo de superfície elevada (`bg-popover`), borda à direita, fixa à esquerda a partir de 1024px.
- Topo: logo (assinatura no claro, só o símbolo no escuro) e, abaixo, o **seletor de board** (nome do board atual; lista com ✓ no atual).
- Navegação (alvo de 44px): **Dashboard** · **Fluxo de caixa** · **Tarefas** · **Estoque**. Item atual com `aria-current="page"`, texto azul, negrito e barra azul à esquerda. Os itens valem para o board escolhido.
- Para admin global, um filete e o item **Gerenciar boards** (`/boards`).
- Rodapé fixo: cartão do usuário (iniciais e nome), **Alternar tema** e **Sair**.
- Sem boards: seletor desabilitado ("Nenhum board") e módulos desabilitados.
- Em `/boards` (sem board na rota) o board escolhido é o primeiro da lista.
- Gaveta (< 1024px): diálogo modal (foco preso, Esc fecha, toque no fundo fecha); fecha ao navegar.

## Cabeçalho

Faixa azul com o botão de menu, o **Sol de Aquário** (só com a sidebar fechada ou no celular: aberta, o logo já está nela), o título da tela em Fraunces (com o nome do board acima, em texto pequeno) e as ações da tela. O hambúrguer, a troca de board e o "Sair" do cabeçalho saem.

## Layout single-screen com a sidebar aberta

A sidebar abre ao lado do conteúdo (não por cima), e o conteúdo **comprime na horizontal**. O grid do fluxo de caixa passa a reagir à largura do **conteúdo** (container queries do Tailwind), não da janela:

- conteúdo ≥ 56rem: três colunas (entradas · saídas · fechamento de 20rem), como hoje;
- de 42rem a 56rem: duas colunas (entradas | saídas) com o fechamento embaixo, largo;
- abaixo de 42rem (celular): empilhado (entradas, saídas, fechamento), cada lista rolando por dentro.

Sem rolagem da página nos dois casos. O cabeçalho também usa container query: o navegador de mês completo aparece com a faixa ≥ 56rem; abaixo vira o ícone de calendário com menu, e "Compartilhar" perde o texto.

## Arquivos

`components/layout/`: `app-shell.tsx` (contexto + sidebar + coluna principal), `app-sidebar.tsx` (conteúdo e gaveta), `board-switcher.tsx`, `page-header.tsx`, `nav-items.ts`, `module-placeholder.tsx`. Novo `components/ui/dropdown-menu.tsx` (shadcn, sobre o `radix-ui` que já é dependência). Sai `components/board/BoardHeader.tsx`.

## Fora de escopo

Os módulos Dashboard, Tarefas e Estoque em si; recolher para só ícones; barra inferior; menu lateral na página pública (`/share`).

## Validação

`pnpm typecheck`, `pnpm lint`, `pnpm build`; capturas de tela com a sidebar aberta e fechada (1440 e ~1100px), gaveta no celular, seletor de board, "Em breve" e os dois temas.

## Checklist

- [x] Rotas: `/fluxo-de-caixa` e redirect de `/board/{id}`; links de mês e `revalidatePath`
- [x] Shell: contexto, sidebar, gaveta, cookie, botão no cabeçalho
- [x] Seletor de board, itens, "Em breve", Gerenciar boards, rodapé
- [x] `PageHeader` e migração das telas (fluxo de caixa, `/boards`, placeholders)
- [x] Grid e cabeçalho por container query
- [x] Validação (build e capturas)
