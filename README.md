# Vitta

Sistema de gestão para corretora de seguros de vida (MAG/Mongeral, Prudential, Icatu, MetLife...):
carteira de clientes e apólices, conciliação mensal das planilhas das seguradoras (quem entrou,
quem saiu e por quê), prospecção de novos clientes e, no futuro, recursos de IA.

## Estrutura (monorepo)

```
Vitta/
├── Vitta.slnx                    solução .NET (só os projetos .NET)
├── Directory.Build.props / Directory.Packages.props   config e versões NuGet centralizadas
├── package.json                  scripts para subir tudo (npm run dev)
├── docker-compose.yml            PostgreSQL opcional (porta 5433) + IA opcional
├── src/
│   ├── Vitta.Domain              entidades, value objects, regras (sem dependências)
│   ├── Vitta.Application         casos de uso (commands/queries via Mediator), portas
│   ├── Vitta.Persistence         EF Core + PostgreSQL, repositórios, migrations
│   ├── Vitta.Infrastructure      leitor de planilhas, cliente HTTP da IA
│   ├── Vitta.API                 minimal APIs, ProblemDetails, OpenAPI/Scalar
│   ├── Vitta.AI                  Python 3.12 · FastAPI · SDK da Anthropic
│   └── Vitta.Client              workspace Angular 22
│       └── projects/
│           ├── core              @vitta/core: modelos + serviços de API (compartilhado)
│           ├── web               app web responsivo (http://localhost:4200)
│           └── mobile            app Ionic 9 + Capacitor (http://localhost:8100)
├── tests/                        Domain, Application e arquitetura (NetArchTest)
└── docs/
```

## Primeira vez

Pré-requisitos: .NET SDK 10, Node 24, Python 3.12. Docker só se quiser rodar com Postgres.

```bash
npm install          # instala o concurrently na raiz
npm run setup        # npm install do client + dotnet tool restore + restore
cd src/Vitta.AI && python3 -m venv .venv && .venv/bin/pip install -e ".[dev]" && cd ../..
```

## Rodando

```bash
npm run dev          # API (SQLite) + web, abre o navegador
npm run dev:all      # API (SQLite) + web + mobile + IA
npm run dev:pg       # API (Postgres no Docker) + web, igual a produção
```

Ou cada parte separada:

| Comando | O que sobe | URL |
|---|---|---|
| `npm run api` | Vitta.API com SQLite (`src/Vitta.API/vitta.db`), hot reload, migrations automáticas | http://localhost:5080/scalar |
| `npm run api:pg` | Vitta.API com PostgreSQL (sobe o Docker antes) | http://localhost:5080/scalar |
| `npm run web` | app web | http://localhost:4200 |
| `npm run mobile` | app Ionic no navegador | http://localhost:8100 |
| `npm run ai` | serviço de IA | http://localhost:8000/docs |

Web e mobile encaminham `/api` para a API via proxy (`projects/web/proxy.conf.json`).
Para IA com LLM: `cp src/Vitta.AI/.env.example src/Vitta.AI/.env` e preencha a chave
(sem chave ela roda em modo offline).

## Testes

```bash
npm test             # .NET + Angular + Python
```

## Banco de dados

| Ambiente | Banco | Configuração |
|---|---|---|
| Development | SQLite (`vitta.db`) | `appsettings.Development.json` |
| Produção | PostgreSQL | `appsettings.json` + variáveis de ambiente |

O banco é escolhido por `Database:Provider` (`Sqlite` ou `Postgres`) e a conexão por
`ConnectionStrings:Vitta`. Em produção, passe por variáveis de ambiente:
`Database__Provider=Postgres` e `ConnectionStrings__Vitta=Host=...;Password=...`.

Para zerar o banco local, apague `src/Vitta.API/vitta.db`; ele é recriado ao subir a API.

## Migrations

Cada banco tem suas migrations (`Migrations/Sqlite` e `Migrations/Postgres`).
Gere sempre para os dois de uma vez:

```bash
npm run migrations:add -- NomeDaMigration
```

## App mobile no celular (quando chegar a hora)

```bash
cd src/Vitta.Client
npm i -D @capacitor/android     # ou @capacitor/ios
npx cap add android
npm run cap:sync && npx cap open android
```
Antes, troque `apiBaseUrl` em `projects/mobile/src/environments/environment.ts` pela URL pública da API.

Veja [docs/architecture.md](docs/architecture.md) para as decisões técnicas e o roadmap.
