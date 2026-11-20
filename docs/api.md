# Contrato da API (v1)

Base: `/api`. JSON em camelCase. Enums trafegam como **string**. Datas `DateOnly` como `"yyyy-MM-dd"`,
`DateTimeOffset` em ISO 8601. Erros seguem ProblemDetails (`title`, `detail`, `status`, `errors`).

**Autenticação:** todos os endpoints exigem `Authorization: Bearer <token>`, exceto `POST /api/auth/login` e `/health`.
401 → token ausente/expirado (o client deve deslogar e mandar para o login).

## Auth

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| POST | `/auth/login` | `{ email, password }` | 200 `LoginResponse` · 401 |
| GET | `/auth/me` | — | 200 `UserDto` |

```ts
LoginResponse { accessToken: string; expiresAt: string; user: UserDto }
UserDto { id: string; name: string; email: string }
```

Usuário inicial em Development: `admin@vitta.local` / `Vitta@2026` (configurável em `Auth:SeedAdmin`).

## Dashboard

`GET /dashboard` → `DashboardDto`

```ts
DashboardDto {
  activeClients: number;          // clientes com ao menos 1 apólice ativa
  totalClients: number;
  activePolicies: number;
  monthlyPremiumTotal: number;    // soma do prêmio mensal das apólices ativas
  openProspects: number;          // estágio diferente de Won/Lost
  followUpsDue: ProspectDto[];    // nextFollowUpOn <= hoje e em aberto, ordenado por data
  prospectsByStage: { stage: ProspectStage; count: number }[];
  policiesByInsurer: { insurerId: string; insurerName: string; count: number; monthlyPremium: number }[];
  recentExits: PortfolioChangeDto[]; // últimas 10 saídas (type = Left) das importações
  lastImportAt: string | null;
}
```

## Clientes

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/clients?search=&page=1&pageSize=20` | — | `PagedResult<ClientListItemDto>` |
| GET | `/clients/{id}` | — | `ClientDetailDto` · 404 |
| POST | `/clients` | `ClientInput & { cpf }` | 201 `{ id }` · 400 · 409 (CPF duplicado) |
| PUT | `/clients/{id}` | `ClientInput` | 204 · 404 |
| DELETE | `/clients/{id}` | — | 204 · 404 · 409 (possui apólices) |

```ts
PagedResult<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
ClientInput { fullName: string; birthDate?: string|null; email?: string|null; phone?: string|null; notes?: string|null }
ClientListItemDto { id; fullName; cpf /* formatado 000.000.000-00 */; phone; email; activePolicyCount: number; monthlyPremium: number }
ClientDetailDto { id; fullName; cpf; birthDate; email; phone; notes; createdAt; policies: PolicyDto[] }
```

## Apólices

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/policies?clientId=&insurerId=&status=&search=&page=1&pageSize=20` | — | `PagedResult<PolicyDto>` |
| POST | `/policies` | `{ clientId, insurerId, policyNumber, productName, monthlyPremium, issuedOn }` | 201 `{ id }` · 400 · 409 (número repetido na seguradora) |
| PUT | `/policies/{id}` | `{ productName, monthlyPremium }` | 204 |
| POST | `/policies/{id}/end` | `{ status: 'Cancelled'|'Lapsed'|'BrokerChanged', endedOn, reason? }` | 204 · 409 (não ativa) |
| POST | `/policies/{id}/reactivate` | — | 204 |

```ts
PolicyStatus = 'Active' | 'Cancelled' | 'Lapsed' | 'BrokerChanged'
PolicyDto { id; clientId; clientName; insurerId; insurerName; policyNumber; productName; monthlyPremium: number;
            issuedOn; status: PolicyStatus; endedOn: string|null; endReason: string|null }
```

## Prospecção

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/prospects?stage=&search=` | — | `ProspectDto[]` |
| POST | `/prospects` | `ProspectInput` | 201 `{ id }` |
| PUT | `/prospects/{id}` | `ProspectInput` | 204 |
| POST | `/prospects/{id}/stage` | `{ stage, nextFollowUpOn?: string|null }` | 204 |
| POST | `/prospects/{id}/convert` | `{ cpf, birthDate?: string|null }` | 201 `{ clientId }` · 409 |
| DELETE | `/prospects/{id}` | — | 204 |

```ts
ProspectStage = 'New' | 'Contacted' | 'MeetingScheduled' | 'ProposalSent' | 'Won' | 'Lost'
ProspectInput { name: string; phone?; email?; source?; notes?; nextFollowUpOn?: string|null }
ProspectDto { id; name; phone; email; source; stage: ProspectStage; nextFollowUpOn: string|null; notes;
              convertedClientId: string|null; createdAt }
```

## Carteira (importação mensal)

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/portfolio/imports` | — | `PortfolioImportDto[]` (mais recente primeiro) |
| GET | `/portfolio/imports/{id}` | — | `PortfolioImportDetailDto` |
| POST | `/portfolio/imports` | `multipart/form-data`: `insurerId`, `referenceMonth` (`yyyy-MM`), `file` (.xlsx ou .csv) | 201 `PortfolioImportDetailDto` · 400 · 409 (mês já importado) |
| DELETE | `/portfolio/imports/{id}` | — | 204 |

```ts
PortfolioChangeType = 'Entered' | 'Left' | 'Changed'
PortfolioImportDto { id; insurerId; insurerName; referenceMonth /* yyyy-MM-01 */; fileName; status; totalRows;
                     entered: number; left: number; changed: number; isFirstImport: boolean; createdAt }
PortfolioImportDetailDto extends PortfolioImportDto { changes: PortfolioChangeDto[] }
PortfolioChangeDto { id; importId; referenceMonth; insurerName; type: PortfolioChangeType; policyNumber; holderName;
                     holderCpf: string|null; detail: string|null; clientId: string|null /* cliente com mesmo CPF */ }
```

Colunas reconhecidas na planilha (cabeçalho na 1ª linha, sem diferenciar maiúsculas/acentos):
apólice/proposta/certificado (**obrigatória**), segurado/nome (**obrigatória**), cpf, produto/plano,
prêmio/valor, status/situação.

## Seguradoras

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/insurers` | — | `InsurerDto[]` `{ id, code, name }` |
| POST | `/insurers` | `{ code, name }` | 201 `{ id }` · 409 |

## Modelos de planilha (templates) — entendidos pela IA

Um **modelo** descreve o layout de uma planilha recorrente (ex.: "Carteira MAG"): qual aba, em que linha está o
cabeçalho e qual coluna corresponde a cada campo do Vitta. É criado uma vez (a IA sugere, o usuário confere) e
reutilizado em toda importação.

```ts
ImportTemplateKind = 'Portfolio' | 'Prospects'
ColumnMapping { field: string; column: string }          // column = texto do cabeçalho na planilha
ImportFieldDto { field: string; label: string; required: boolean; multiple: boolean; description: string }
ImportTemplateDto { id; name; kind: ImportTemplateKind; insurerId: string|null; insurerName: string|null;
                    sheetName: string|null; headerRow: number /* 1 = primeira linha; 0 = sem cabeçalho */; columns: ColumnMapping[];
                    createdByAi: boolean; aiNotes: string|null; createdAt; lastUsedAt: string|null }
ImportTemplateInput { name; kind; insurerId?: string|null; sheetName?: string|null; headerRow: number;
                      columns: ColumnMapping[]; createdByAi?: boolean; aiNotes?: string|null }
SpreadsheetAnalysisDto {
  fileName: string;
  sheets: string[];              // abas do .xlsx ([] para .csv)
  sheetName: string|null;        // aba analisada
  rows: string[][];              // primeiras 25 linhas "cruas" da aba (para pré-visualizar e trocar a linha do cabeçalho)
  headerRow: number;             // sugerida (1-based; 0 = sem cabeçalho)
  columns: ColumnMapping[];      // sugestão de mapeamento (só colunas que existem na linha do cabeçalho)
  suggestedName: string;
  notes: string|null;            // observações da IA (ex.: "a coluna Situação usa ATIVO/CANCELADO")
  confidence: 'high'|'medium'|'low';
  analyzedBy: 'ai' | 'heuristic' // 'heuristic' = IA sem chave/indisponível; mapeamento por nomes conhecidos
}
```

Campos por tipo (servidos por `GET /import-templates/fields?kind=`):
- **Portfolio**: `policyNumber` (apólice), `holderName`* (segurado), `holderCpf`, `productName`, `monthlyPremium`,
  `status`, `startDate` (início da vigência). Obrigatório também: `policyNumber` **ou** `holderCpf`. Sem nº da apólice,
  a linha é identificada por `"CPF 999.999.999-99 · dd/mm/aaaa"` (CPF + início da vigência) para comparar os meses.
- **Prospects**: `name`*, `phone`, `email`, `source`, `nextFollowUpOn`, `notes` (`multiple`: várias colunas viram "Coluna: valor" nas observações).

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/import-templates?kind=` | — | `ImportTemplateDto[]` (ordenado por nome) |
| GET | `/import-templates/fields?kind=` | — | `ImportFieldDto[]` |
| GET | `/import-templates/{id}` | — | `ImportTemplateDto` · 404 |
| POST | `/import-templates/analyze` | `multipart/form-data`: `file`, `kind`, `sheetName?` | 200 `SpreadsheetAnalysisDto` · 400 |
| POST | `/import-templates` | `ImportTemplateInput` | 201 `{ id }` · 400 (campo obrigatório sem coluna) · 409 (nome repetido) |
| PUT | `/import-templates/{id}` | `ImportTemplateInput` | 204 · 400 · 404 · 409 |
| DELETE | `/import-templates/{id}` | — | 204 · 404 |

Importação com modelo:
- `POST /portfolio/imports` aceita também `templateId` (opcional). Com modelo, `insurerId` pode ser omitido se o
  modelo tiver seguradora. Sem modelo, continua a detecção automática pelos nomes de coluna.
- `POST /prospects/imports` — `multipart/form-data`: `file`, `templateId` → 200
  `ProspectImportResultDto { totalRows: number; created: number; skipped: number; messages: string[] }`.
  Cria prospects na etapa **Novo**; pula duplicados (mesmo e-mail, mesmo telefone ou mesmo nome sem contato).
- **Texto colado do site** (ex.: MAG, que não exporta planilha): o client envia o texto como arquivo
  `texto-colado.txt`. A API separa as células por TAB (ou por 2+ espaços), ignora linhas em branco e junta a célula
  que o navegador quebrou numa linha sozinha (ex.: "Ativo"). Sem linha de títulos, `headerRow = 0` e as colunas se
  chamam `"Coluna 1"`, `"Coluna 2"`... (pela posição). Ao importar com um modelo assim, a API confere o formato dos
  valores (CPF, data, valor, e-mail) e devolve 400 `ImportTemplate.LayoutChanged` se a ordem das colunas mudou.
- Se a planilha não tiver uma coluna obrigatória do modelo → 400 com `detail` explicando qual coluna faltou
  (o layout pode ter mudado: crie/ajuste o modelo).
