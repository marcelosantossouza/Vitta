# Arquitetura e decisões

## Camadas (backend)

```
API ──► Application ──► Domain
 │           ▲
 ├──► Persistence (implementa repositórios / IUnitOfWork)
 └──► Infrastructure (implementa IAiService, IPortfolioSpreadsheetReader)
```

- **Domain**: `Client`, `Policy`, `Insurer`, `Prospect`, `PortfolioImport`/`PortfolioChange`,
  value object `Cpf`, `Result`/`Error`, e o serviço de domínio `PortfolioReconciler`
  (compara a carteira do mês anterior com a atual → Entrou / Saiu / Mudou).
- **Application**: um arquivo por caso de uso (`Features/<Contexto>/<CasoDeUso>`), contendo
  command/query + validador + handler. Define as portas (interfaces) que as camadas externas implementam.
- **Persistence** e **Infrastructure** separadas: banco de um lado, integrações externas do outro.
- Os testes em `Vitta.ArchitectureTests` quebram o build se alguém violar a regra de dependência.

Foi adicionada a camada **Domain** (não estava na lista original): é o núcleo da Clean Architecture
e mantém as regras de negócio (CPF, status de apólice, conciliação) livres de EF e de frameworks.

## MediatR → Mediator

O MediatR passou a ter licença comercial a partir da v13 (2025). Opções avaliadas:

| Opção | Prós | Contras |
|---|---|---|
| MediatR 12.x (último Apache 2.0) | zero mudança de hábito | congelado, sem correções futuras |
| MediatR 13+ | mantido | licença comercial / chave de licença |
| **martinothamar/Mediator** (MIT) | API quase idêntica, source generator (sem reflection, mais rápido), pipeline behaviors | handlers retornam `ValueTask` |
| Dispatcher próprio | zero dependência | mais código para manter |

**Escolhido: `Mediator` 3.x (martinothamar).** O uso é o mesmo que você já conhece:

```csharp
public sealed record CreateClientCommand(...) : ICommand<Result<Guid>>;
public sealed class CreateClientHandler : ICommandHandler<CreateClientCommand, Result<Guid>> { ... }
// na API
await sender.Send(new CreateClientCommand(...), ct);   // ISender, igual ao MediatR
```

Diferenças: `ICommand`/`IQuery`/`IRequest` em vez só de `IRequest`; `ValueTask` em vez de `Task`;
o registro (`AddMediator`) fica no projeto host (Vitta.API), onde o source generator roda.

Pelo mesmo motivo (licença comercial) **não usamos AutoMapper** — mapeamento manual
(`ClientDto.From(client)`), que é explícito e simples para o tamanho do projeto.

## Banco de dados: SQLite (dev) e PostgreSQL (prod)

- `Context/VittaDbContext` é **abstrato e independente de banco**. Repositórios dependem só dele.
- `SqliteVittaDbContext` e `PostgresVittaDbContext` são subclasses finas. Elas existem para que cada
  provedor tenha **suas próprias migrations** (o SQL gerado é diferente) e seus ajustes:
  - SQLite: `DateTimeOffset` gravado como long e `decimal` como double, porque o SQLite não ordena
    nem soma esses tipos no SQL. Para um banco de desenvolvimento a precisão de double é suficiente.
  - Postgres: schema `vitta` e tipos nativos (`timestamptz`, `numeric`).
- `Database:Provider` escolhe qual contexto é registrado. `IDesignTimeDbContextFactory` permite gerar
  migrations dos dois independentemente da configuração atual.
- **Consultas devem ser portáveis.** Nada de `EF.Functions.ILike` (só Postgres). Para busca por nome,
  o domínio mantém `Client.SearchName` normalizado (minúsculo, sem acento), que funciona igual nos dois bancos.
- **Atenção:** SQLite é mais permissivo que o Postgres (tipos, tamanho de colunas). Antes de publicar,
  rode `npm run dev:pg` para testar contra o banco de produção.

### Por que não MariaDB (por enquanto)

O provedor Pomelo (padrão para MySQL/MariaDB) ainda não tem versão para EF Core 10. O provedor da
Oracle (`MySql.EntityFrameworkCore`) suporta EF 10, mas é feito para MySQL e não garante MariaDB.
Se um dia for necessário: criar `MariaDbVittaDbContext` + `Migrations/MariaDb` + um `case` no
`DependencyInjection`. O resto do sistema não muda.

## Erros

- Regras de negócio retornam `Result`/`Error` (sem exceptions) → a API converte em ProblemDetails
  (400/404/409).
- Validação de entrada via FluentValidation no `ValidationBehavior` → 400 com `errors` por campo.

## Frontend

- Angular workspace em `src/Vitta.Client` com três projetos:
  - `core` (lib sem UI: modelos, serviços HTTP, config) — alias `@vitta/core` aponta direto para o fonte, sem build.
  - `web` — app web responsivo (sem lib de UI).
  - `mobile` — Ionic 9 (componentes standalone de `@ionic/angular`) + Capacitor, com abas.
- Regra: tudo que não é visual (modelos, chamadas à API, regras de tela reaproveitáveis) vai para `core`.
- Componentes standalone, signals, control flow (`@if`/`@for`), lazy loading por rota.
- Layout responsivo sem lib de UI: menu lateral no desktop, drawer no mobile, tabelas que viram cards.

## IA

Serviço Python (FastAPI) em `src/Vitta.AI`, chamado pela API via `IAiService` (HTTP). Assim o .NET não depende
de bibliotecas de IA e o Python pode evoluir sozinho (leitura inteligente de planilhas com layouts
diferentes, resumo mensal da carteira, sugestões de prospecção). Sem chave configurada responde em modo offline.

## Roadmap sugerido

1. Autenticação (usuário único no início: ASP.NET Identity + JWT, ou um provedor externo).
2. Apólices: CRUD vinculado a cliente e seguradora.
3. **Importação de carteira MAG**: upload da planilha → `PortfolioReconciler` → alertas de saída.
   Precisa de uma planilha real (anonimizada) para mapear as colunas.
4. Prospecção: kanban + lembretes de follow-up.
5. Dashboard com indicadores (carteira ativa, prêmio mensal, saídas, comissões).
6. Deploy (ex.: Azure Container Apps / Fly.io + Postgres gerenciado).
7. App Ionic.
