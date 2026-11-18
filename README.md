# Vitta

Sistema de gestão para corretora de seguros de vida (MAG/Mongeral, Prudential, Icatu, MetLife...):
carteira de clientes e apólices, conciliação mensal das planilhas das seguradoras (quem entrou,
quem saiu e por quê), prospecção de novos clientes e, no futuro, recursos de IA.

## Estrutura (monorepo)

```
Vitta/
├── backend/                  .NET 10 · Clean Architecture
│   ├── Vitta.slnx
│   ├── src/
│   │   ├── Vitta.Domain          entidades, value objects, regras (sem dependências)
│   │   ├── Vitta.Application     casos de uso (commands/queries via Mediator), portas
│   │   ├── Vitta.Persistence     EF Core + PostgreSQL, repositórios, migrations
│   │   ├── Vitta.Infrastructure  serviços externos: leitor de planilhas, cliente HTTP da IA
│   │   └── Vitta.API             minimal APIs, ProblemDetails, OpenAPI/Scalar
│   └── tests/                Domain, Application e testes de arquitetura (NetArchTest)
├── client/                   Angular 22 workspace
│   └── projects/
│       ├── core              @vitta/core: modelos + serviços de API (compartilhado)
│       ├── web               app web responsivo
│       └── mobile            (reservado) app Ionic + Capacitor
├── ai/                       Python 3.12 · FastAPI · SDK da Anthropic
├── docs/                     decisões de arquitetura
└── docker-compose.yml        PostgreSQL (+ serviço de IA opcional)
```

## Rodando localmente

Pré-requisitos: .NET SDK 10, Node 24, Python 3.12, Docker.

```bash
# 1. Banco (Postgres na porta 5433)
docker compose up -d postgres

# 2. API  →  http://localhost:5080/scalar   (migrations aplicadas automaticamente em Development)
cd backend
dotnet tool restore
dotnet run --project src/Vitta.API

# 3. Web  →  http://localhost:4200   (/api é encaminhado para a API via proxy)
cd client
npm install
npm start

# 4. IA (opcional)  →  http://localhost:8000/docs
cd ai
python3 -m venv .venv && .venv/bin/pip install -e ".[dev]"
cp .env.example .env   # coloque VITTA_AI_ANTHROPIC_API_KEY (sem chave roda em modo offline)
.venv/bin/uvicorn app.main:app --reload
```

## Testes

```bash
cd backend && dotnet test
cd client  && npx ng test web --watch=false
cd ai      && .venv/bin/pytest
```

## Migrations

```bash
cd backend
dotnet ef migrations add <Nome> -p src/Vitta.Persistence -s src/Vitta.API -o Migrations
```

Veja [docs/architecture.md](docs/architecture.md) para as decisões técnicas e o roadmap.
