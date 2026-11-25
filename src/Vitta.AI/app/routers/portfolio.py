from fastapi import APIRouter, Depends
from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel

from app.config import Settings, get_settings
from app.llm import LlmChoice, LlmClient, LlmUnavailableError

router = APIRouter(prefix="/v1/portfolio", tags=["portfolio"])


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class SummarizeRequest(CamelModel):
    insurer_name: str
    reference_month: str
    changes: list[str]
    llm: LlmChoice | None = None


class SummarizeResponse(CamelModel):
    summary: str


SYSTEM_PROMPT = (
    "Voce e assistente de um corretor de seguros de vida no Brasil. "
    "Resuma em portugues, de forma objetiva, as mudancas de carteira do mes, "
    "destacando clientes que sairam (possivel troca de corretor) e acoes sugeridas."
)


@router.post("/summarize", response_model=SummarizeResponse, response_model_by_alias=True)
def summarize(req: SummarizeRequest, settings: Settings = Depends(get_settings)) -> SummarizeResponse:
    llm = LlmClient(settings, req.llm)
    fallback = SummarizeResponse(
        summary=f"{len(req.changes)} mudanca(s) na carteira {req.insurer_name} em {req.reference_month}."
    )
    if not llm.enabled:
        return fallback
    prompt = f"Seguradora: {req.insurer_name}\nMes: {req.reference_month}\nMudancas:\n" + "\n".join(
        f"- {c}" for c in req.changes
    )
    try:
        return SummarizeResponse(summary=llm.complete(SYSTEM_PROMPT, prompt))
    except LlmUnavailableError:
        return fallback
