# Spec de Identidade Visual — alinhada ao sistema do IGB

Status: aprovado em 2026-11-25. Referência: `~/Code/igb-sistema/frontend` (tokens em `apps/web/src/styles/tokens.css`, guia em `docs/brand/brand-guide.md`). O guia oficial do IGB manda; o que ele não define vem do que o buscador e o sistema do IGB já fazem.

## Decisões

- **Abordagem:** manter o shadcn/ui e **remapear** as variáveis dele (`--primary`, `--background`…) para as cores da marca. Não se portam os componentes do IGB (que dependem do i18n e de outras peças deles). Sem dependência nova.
- **Densidade:** marca do IGB, densidade do Citros. O dashboard continua compacto (tela única, sem rolagem). Modais, login e a tela de boards usam a escala do IGB (texto 16px, alvos de 44px). Em telas de toque (`pointer: coarse`) todos os alvos sobem para 44px.
- **Tema:** claro por padrão e **escuro automático** pelo sistema (`prefers-color-scheme`), com **botão** para alternar à mão (a escolha fica salva e vale até ser trocada de novo). A tecla `D` continua funcionando.
- **Imagem de fechamento (PNG):** também na identidade do IGB.

## Cores (HEX só em `src/styles/tokens.css`)

| Token (shadcn) | Claro | Escuro | Uso |
|---|---|---|---|
| `--background` | creme `#F5F2ED` | `#0F172A` | fundo da página |
| `--foreground` | `#0F172A` | `#DBE4F0` | texto |
| `--card` | branco | `#1E293B` | cards, campos |
| `--popover` | branco | `#273449` | modais e menus |
| `--primary` | amarelo `#ECC805` | idem | CTA; texto escuro `#0F172A` sobre ele (10,9:1) |
| `--secondary` / `--brand` | azul `#2350A0` | idem | estrutura (cabeçalho); texto branco (7,7:1) |
| `--muted-foreground` | `#475569` | `#A7B4C6` | texto secundário |
| `--border` | `#E2E8F0` | `#334155` | divisores decorativos |
| `--input` | `#64748B` | `#94A3B8` | contorno de campo (≥ 3:1) |
| `--accent-ink` | azul `#2350A0` | `#A7B9D9` | link, ativo, valor positivo |
| `--destructive` (texto) | vermelho `#C84119` | `#E4A08C` | erro, valor negativo |
| `--danger` (fundo) | `#C84119` | idem | botão perigoso; texto branco (4,97:1) |
| `--ring` | azul | amarelo | anel de foco |

Regras de contraste do guia: o amarelo **nunca** é cor de texto sobre fundo claro; sobre o cabeçalho azul o anel de foco é amarelo. Valores positivos usam o azul e negativos o vermelho (a paleta da marca não tem verde).

## Tipografia

- **Avenir** (stack nativa `Avenir Next, Avenir, system-ui…`) para todo o texto. Os arquivos licenciados ainda não existem (pendência do IGB).
- **Fraunces** (`next/font/google`, auto-hospedada no build) só em `h1`/`h2` de tela e palavra de destaque. Títulos de painel, de modal e de card são texto de interface: `font-sans`.

## Forma

Botão em **pílula**; campo `14px`; card `16px`; modal e painel `24px`; sombra tingida de azul (`shadow-card`). Botão primário amarelo, secundário com contorno azul de 2px (preenche de azul no hover), perigo vermelho, `ghost` neutro. Foco visível global: contorno de 2px afastado 2px. Cursor de mãozinha em todo clicável; `not-allowed` no indisponível. Animações respeitam `prefers-reduced-motion`.

## Telas

- **Cabeçalho** (board, `/boards`, link público): faixa azul com o **Sol de Aquário** em círculo branco, título em Fraunces e controles em branco (anel de foco amarelo). No tema escuro continua azul.
- **Login:** card de 24px, assinatura do IGB no claro e só o símbolo no escuro, título em Fraunces, "Entrar" amarelo e Google em contorno azul.
- **Dashboard:** painéis como cards de 16px com sombra; entradas em azul e saídas em vermelho; saldo final positivo em azul, negativo em vermelho.
- **Modais:** `Dialog` com 24px, título em Avenir, campos de 44px e botão de fechar de 44px.
- **Menu do cabeçalho:** ganha o botão de alternar tema.
- **Favicon e barra do navegador:** Sol de Aquário e `theme-color` azul `#2350A0`.

## Imagem de fechamento

Cabeçalho azul com o Sol de Aquário em círculo branco, título "Fluxo de caixa" em Fraunces e nome do board/período em branco; fundo creme; cards brancos de 24px; cabeçalho de **Entradas** azul e de **Saídas** vermelho (texto branco nos dois); saldo final positivo em azul e negativo em vermelho, com faixa amarela de destaque. Sem emojis. Fraunces só é usada se já estiver carregada no navegador; senão cai na serifa do sistema.

## Assets

Copiados de `igb-sistema/frontend/apps/web/public/brand/` para `apps/web/public/brand/`: `assinatura-2026-640.png`, `sol-de-aquario-{32,128,192,512}.png`, `apple-touch-icon-180.png`.

## Fora de escopo

Avenir licenciada (pendência do IGB), i18n, menu lateral/barra inferior do IGB (o dashboard do Citros é de tela única), versão negativa da assinatura.

## Validação

`pnpm typecheck`, `pnpm lint`, `pnpm build`; capturas de tela de login, dashboard, modais e `/boards` nos dois temas; conferência dos contrastes da tabela acima.

## Checklist

- [x] Tokens, fontes e CSS global (`tokens.css`, `globals.css`, `fonts.ts`), assets de marca, tema com alternância
- [x] Componentes base: `button`, `dialog`, novo `input`
- [x] Cabeçalhos (board, `/boards`, link público) e menu com alternar tema
- [x] Login
- [x] Dashboard (painéis, modais, formulários)
- [x] Imagem de fechamento
- [x] Validação (build e capturas de tela)
