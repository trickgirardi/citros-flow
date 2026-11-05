# Spec de Deploy — Dokploy

Status: aprovado em 2026-11-05 (formato A, migrations no startup, `output: "standalone"`).

## Decisões

- **Formato:** 2 *Applications* no Dokploy (`api` e `web`, cada uma com seu Dockerfile) + 1 *Database* PostgreSQL gerenciada pelo Dokploy (backup agendado para S3 pela UI).
- **Domínio:** `dokploy.trickgs.dev.br` (a confirmar: se for o host do painel do Dokploy, usar outro subdomínio para o app).
- **Roteamento (Traefik do Dokploy), mesmo host:**
  - `web`: path `/`, porta `3000`
  - `api`: path `/api`, porta `8080` (sem strip path: a API já responde em `/api/*`)
  - HTTPS com Let's Encrypt nos dois.
- **Migrations:** `Database__MigrateOnStartup=true` em produção (uma única réplica da API).
- **Next:** `output: "standalone"` em `apps/web/next.config.ts`.

## Arquivos

| Arquivo | Função |
|---|---|
| `apps/api/Dockerfile` | multi-stage (sdk → aspnet), porta 8080, usuário não-root, `/keys` para o Data Protection |
| `apps/api/.dockerignore` | exclui `bin/`, `obj/`, `tests/` |
| `apps/web/Dockerfile` | multi-stage (deps → build → runner standalone), porta 3000 |
| `apps/web/.dockerignore` | exclui `node_modules`, `.next`, `.env*` |

Build path no Dokploy: `apps/api` e `apps/web` (Dockerfile na raiz de cada um, contexto = a própria pasta).

## Passo a passo no Dokploy

### 1. Banco
*Create Service → Database → PostgreSQL.* Imagem `postgres:17` (o Dokploy monta o volume em `/var/lib/postgresql/data`, caminho que o Postgres 18 não aceita; o schema não usa nada exclusivo do 18). Anote usuário, senha, database e o **internal host** (appName). Depois configure *Backups* (destino S3 + schedule).

### 2. API (Application, Git, build path `apps/api`, Dockerfile)
Environment:
```
ConnectionStrings__Default=Host=<appName do banco>;Port=5432;Database=<db>;Username=<user>;Password=<senha>
Database__MigrateOnStartup=true
App__PublicOrigin=https://dokploy.trickgs.dev.br
Google__ClientId=<client id de produção>
Google__ClientSecret=<secret de produção>
```
- *Volumes:* volume nomeado montado em `/keys` (chaves do Data Protection; sem ele toda redeploy derruba as sessões).
- *Domains:* host acima, path `/api`, porta `8080`, HTTPS.

### 3. Web (Application, Git, build path `apps/web`, Dockerfile)
*Build Args* **e** Environment (a variável é gravada no build e lida em runtime):
```
API_INTERNAL_URL=http://<appName da api>:8080
```
- *Domains:* host acima, path `/`, porta `3000`, HTTPS.

### 4. Google OAuth
Client **de produção** (separado do dev), redirect URI exata: `https://dokploy.trickgs.dev.br/api/auth/signin-google`.

### 5. Seed (uma vez, depois da primeira subida da API)
Sem seed ninguém loga. No *Terminal* do container do Postgres no Dokploy:
```bash
psql -U <user> -d <db> -v ON_ERROR_STOP=1
```
e cole o conteúdo do `seed.sql` real (derivado de `infra/postgres/seed.example.sql`, e-mails em minúsculas).

### 6. Verificação
- `https://<domínio>/api/health` → `{"status":"ok"}`
- `https://<domínio>/` → redireciona para `/login`
- Login com e-mail do seed → board abre.
- Redeploy da API e confirmar que a sessão continua válida (volume `/keys`).

## Fora de escopo
CI/CD além do auto-deploy do Dokploy por push, healthcheck do Docker (a imagem `aspnet` não tem curl; usar o healthcheck do Dokploy/Traefik se necessário), múltiplas réplicas da API.
