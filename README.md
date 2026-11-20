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
│           ├── web               app web responsivo (http://localhost:4300)
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
| `npm run web` | app web | http://localhost:4300 |
| `npm run mobile` | app Ionic no navegador | http://localhost:8100 |
| `npm run ai` | serviço de IA | http://localhost:8000/docs |

Web e mobile encaminham `/api` para a API via proxy (`projects/web/proxy.conf.json`).
Para IA com LLM: `cp src/Vitta.AI/.env.example src/Vitta.AI/.env` e preencha a chave
(sem chave ela roda em modo offline).

### Modelos de planilha (IA)

Em **Modelos de planilha** o corretor envia uma planilha de exemplo (carteira da seguradora ou lista de
prospects). A camada de IA encontra a linha do cabeçalho e sugere qual coluna alimenta cada campo; ele confere,
ajusta e salva. Depois, em **Carteira** e em **Prospecção → Importar planilha**, basta escolher o modelo.

- **Sem IA** (sem chave ou serviço fora do ar), a API sugere o mapeamento pelos nomes de coluna conhecidos —
  a tela avisa "IA indisponível".
- **Privacidade:** para a IA vão só as primeiras 25 linhas, com todos os **dígitos trocados por 9** (CPF,
  telefone, valores) e e-mails substituídos. Nomes continuam visíveis. A IA responde o *número* da coluna e a
  API resolve o nome real localmente.
- Se a seguradora mudar o layout, a importação avisa qual coluna sumiu: edite o modelo ou crie outro.
- **Colar do site:** quando a seguradora não exporta planilha (MAG), copie a tabela da página e cole no campo
  "Colar do site". Sem linha de títulos, as colunas são reconhecidas pelo conteúdo (CPF, data, valor...) e guardadas
  pela posição. Sem nº da apólice, a comparação entre meses usa o CPF + início da vigência.

### Login

Em desenvolvimento o primeiro usuário é criado automaticamente:

- **E-mail:** `admin@vitta.local`
- **Senha:** `Vitta@2026`

Para testar a importação de carteira há planilhas fictícias em [docs/exemplos](docs/exemplos/README.md).

## O que já funciona

| Área | Funcionalidades |
|---|---|
| Acesso | Tela de abertura, login (JWT), logout |
| Painel | Clientes/apólices ativos, prêmio mensal total, follow-ups vencidos, funil, carteira por seguradora, últimas saídas |
| Clientes | Cadastro, edição, exclusão, busca por nome (sem acento) ou CPF, detalhe com apólices |
| Apólices | Cadastro por cliente, edição, encerrar (cancelada / caducada / troca de corretor), reativar, lista com filtros |
| Prospecção | Funil (Novo → Contatado → Reunião → Proposta → Ganho/Perdido), próximo follow-up, converter em cliente |
| Carteira | Upload mensal (.xlsx/.csv ou texto colado do site) por seguradora, comparação com o mês anterior: entrou / saiu / mudou |
| Seguradoras | Lista e cadastro |

O contrato completo da API está em [docs/api.md](docs/api.md).

## Testes

```bash
npm test             # testes unitários: .NET + Angular + Python
npm run smoke        # teste ponta a ponta contra a API rodando (grava dados de teste!)
```

## Banco de dados

| Ambiente | Banco | Configuração |
|---|---|---|
| Development | SQLite (`vitta.db`) | `appsettings.Development.json` |
| Produção | PostgreSQL | `appsettings.json` + variáveis de ambiente |

### Produção: configurações obrigatórias

| Variável | Exemplo |
|---|---|
| `Database__Provider` | `Postgres` |
| `ConnectionStrings__Vitta` | `Host=...;Database=vitta;Username=...;Password=...` |
| `Auth__Jwt__Key` | segredo aleatório com **32+ caracteres** (a API não sobe sem ele) |
| `Auth__SeedAdmin__Email` / `__Password` / `__Name` | usuário inicial (só é criado se não existir nenhum) |

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
