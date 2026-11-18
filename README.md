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
├── docker-compose.yml            PostgreSQL (porta 5433) + IA opcional
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

Pré-requisitos: .NET SDK 10, Node 24, Python 3.12, Docker.

```bash
npm install          # instala o concurrently na raiz
npm run setup        # npm install do client + dotnet tool restore + restore
cd src/Vitta.AI && python3 -m venv .venv && .venv/bin/pip install -e ".[dev]" && cd ../..
```

## Rodando

```bash
npm run dev          # Postgres + API + web (abre o navegador)
npm run dev:all      # Postgres + API + web + mobile + IA
```

Ou cada parte separada:

| Comando | O que sobe | URL |
|---|---|---|
| `npm run db` | PostgreSQL (Docker) | localhost:5433 |
| `npm run api` | Vitta.API com hot reload (migrations automáticas em Development) | http://localhost:5080/scalar |
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

## Migrations

```bash
dotnet ef migrations add <Nome> -p src/Vitta.Persistence -s src/Vitta.API -o Migrations
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
