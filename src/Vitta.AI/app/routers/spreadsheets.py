"""Entende o layout de uma planilha (carteira da seguradora, lista de prospects) para criar um modelo de importacao."""

import json
from typing import Literal

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

from app.config import Settings, get_settings
from app.llm import LlmClient, LlmUnavailableError

router = APIRouter(prefix="/v1/spreadsheets", tags=["spreadsheets"])


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class TargetField(CamelModel):
    field: str
    label: str
    required: bool
    multiple: bool
    description: str
    synonyms: list[str] = []


class AnalyzeRequest(CamelModel):
    kind: Literal["Portfolio", "Prospects"]
    file_name: str
    sheet_name: str | None = None
    rows: list[list[str]] = Field(max_length=60)
    fields: list[TargetField]


class SuggestedColumn(CamelModel):
    field: str
    column_number: int


class AnalyzeResponse(CamelModel):
    header_row: int
    columns: list[SuggestedColumn]
    suggested_name: str
    notes: str
    confidence: Literal["high", "medium", "low"]


# Formato que o Claude devolve (validado pelo SDK).
class _Mapping(BaseModel):
    field: str = Field(description="Chave do campo do Vitta (ex.: policyNumber)")
    column_number: int = Field(description="Numero da coluna na planilha, comecando em 1 (coluna A = 1)")


class _Analysis(BaseModel):
    header_row: int = Field(description="Linha do cabecalho, comecando em 1; 0 quando NAO ha cabecalho (a linha 1 ja e de dados)")
    columns: list[_Mapping]
    suggested_name: str = Field(description="Nome curto para o modelo, ex.: 'Carteira MAG'")
    notes: str = Field(description="Observacoes curtas para o corretor, em portugues")
    confidence: Literal["high", "medium", "low"]


KIND_CONTEXT = {
    "Portfolio": (
        "planilha mensal de CARTEIRA enviada por uma seguradora de vida (ex.: MAG/Mongeral, Prudential) "
        "ao corretor. Cada linha e uma apolice/proposta do corretor. Ela sera comparada com a do mes anterior "
        "para descobrir quem entrou, quem saiu e o que mudou."
    ),
    "Prospects": (
        "lista de POSSIVEIS CLIENTES (prospeccao) de um corretor de seguros de vida. Cada linha e uma pessoa "
        "a ser contatada."
    ),
}

SYSTEM_PROMPT = """Voce analisa planilhas de um corretor de seguros de vida no Brasil para que o sistema Vitta \
consiga importa-las sozinho todo mes. Sua tarefa e entender o layout:

1. Encontre a linha do cabecalho (onde estao os nomes das colunas). Planilhas de seguradora costumam ter \
titulos, logotipos ou filtros nas primeiras linhas; o cabecalho e a linha logo acima dos dados. Muitas vezes o \
corretor COPIA a tabela do site da seguradora e cola no Vitta, sem a linha de titulos: se a primeira linha ja for \
de dados, use header_row = 0 e identifique cada coluna so pelo conteudo.
2. Para cada campo do Vitta, indique o numero da coluna (A = 1) que o alimenta, olhando o nome da coluna E os \
valores de exemplo. So mapeie quando fizer sentido; nunca invente. Campos com "multiple" podem receber varias \
colunas (uma entrada por coluna) - use para guardar informacoes uteis que nao tem campo proprio.
3. Cada coluna alimenta no maximo um campo. Campos obrigatorios devem ser mapeados se existir coluna plausivel.
4. Uma coluna com o MESMO valor em todas as linhas (ex.: codigo do corretor ou da agencia) nao identifica a linha: \
nunca a use como numero de apolice. Se nao houver numero de apolice, deixe policyNumber sem mapear - o Vitta usa o \
CPF (e o inicio da vigencia) para comparar os meses; mencione isso em "notes".

Os valores numericos foram mascarados (todo digito virou 9) e e-mails trocados por exemplo, para proteger \
dados pessoais: use o FORMATO dos valores (ex.: 999.999.999-99 e um CPF; 99/99/9999 e uma data; \
R$ 9.999,99 e um valor).

Em "notes" escreva, em portugues e em no maximo 3 frases curtas, o que o corretor deve conferir (ex.: colunas \
parecidas, valores de situacao encontrados como ATIVO/CANCELADO, campos que nao encontrou). \
"confidence" = high quando o cabecalho e os campos obrigatorios estao claros."""


def _render_rows(rows: list[list[str]]) -> str:
    return "\n".join(f"Linha {i}: " + json.dumps(row, ensure_ascii=False) for i, row in enumerate(rows, start=1))


def build_prompt(req: AnalyzeRequest) -> str:
    fields = [f.model_dump(include={"field", "label", "required", "multiple", "description"}) for f in req.fields]
    return (
        f"Tipo: {KIND_CONTEXT[req.kind]}\n"
        f"Arquivo: {req.file_name}" + (f" (aba \"{req.sheet_name}\")" if req.sheet_name else "") + "\n\n"
        f"Campos do Vitta:\n{json.dumps(fields, ensure_ascii=False, indent=1)}\n\n"
        f"Primeiras linhas da planilha (cada linha e uma lista de celulas, da coluna A em diante):\n"
        f"{_render_rows(req.rows)}"
    )


@router.post("/analyze", response_model=AnalyzeResponse, response_model_by_alias=True)
def analyze(req: AnalyzeRequest, settings: Settings = Depends(get_settings)) -> AnalyzeResponse:
    llm = LlmClient(settings)
    if not llm.enabled:
        # A Vitta.API trata 503 como "IA indisponivel" e usa a heuristica por nomes de coluna.
        raise HTTPException(status_code=503, detail="IA em modo offline (sem VITTA_AI_ANTHROPIC_API_KEY).")

    try:
        result = llm.parse(SYSTEM_PROMPT, build_prompt(req), _Analysis)
    except LlmUnavailableError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc

    known = {f.field for f in req.fields}
    width = max((len(r) for r in req.rows), default=0)
    return AnalyzeResponse(
        header_row=min(max(result.header_row, 0), len(req.rows)),
        columns=[
            SuggestedColumn(field=m.field, column_number=m.column_number)
            for m in result.columns
            if m.field in known and 1 <= m.column_number <= width
        ],
        suggested_name=result.suggested_name.strip()[:120],
        notes=result.notes.strip()[:2000],
        confidence=result.confidence,
    )
