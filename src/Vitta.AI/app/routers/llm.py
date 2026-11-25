"""Provedores de IA disponiveis e teste de conexao, para a tela de configuracao da Vitta."""

import time

from fastapi import APIRouter, Depends
from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel

from app.config import Settings, get_settings
from app.llm import LlmChoice, LlmClient, LlmUnavailableError

router = APIRouter(prefix="/v1/llm", tags=["llm"])


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class ModelOption(CamelModel):
    id: str
    label: str
    note: str


class ProviderInfo(CamelModel):
    id: str
    label: str
    configured: bool
    default_model: str
    models: list[ModelOption]


class ProvidersResponse(CamelModel):
    default_provider: str
    default_effort: str
    providers: list[ProviderInfo]


class TestResponse(CamelModel):
    ok: bool
    provider: str
    model: str
    latency_ms: int | None
    message: str


# Sugestoes para a tela. O campo de modelo aceita qualquer id: o botao "Testar conexao" confirma se existe.
ANTHROPIC_MODELS = [
    ModelOption(id="claude-haiku-5-5", label="Claude Haiku 5.5", note="Mais barato e rápido. Bom para o dia a dia."),
    ModelOption(id="claude-sonnet-5-5", label="Claude Sonnet 5.5", note="Equilíbrio entre custo e qualidade."),
    ModelOption(id="claude-opus-5-5", label="Claude Opus 5.5", note="Mais capaz e mais caro."),
]
GROQ_MODELS = [
    ModelOption(id="openai/gpt-oss-120b", label="GPT-OSS 120B", note="Modelo aberto grande, com raciocínio."),
    ModelOption(id="openai/gpt-oss-20b", label="GPT-OSS 20B", note="Mais leve e barato."),
    ModelOption(id="llama-3.3-70b-versatile", label="Llama 3.3 70B", note="Rápido, sem raciocínio."),
]


class _Ping(BaseModel):
    ok: bool


@router.get("/providers", response_model=ProvidersResponse, response_model_by_alias=True)
def providers(settings: Settings = Depends(get_settings)) -> ProvidersResponse:
    return ProvidersResponse(
        default_provider="groq" if settings.provider == "groq" else "anthropic",
        default_effort=settings.effort,
        providers=[
            ProviderInfo(
                id="anthropic", label="Anthropic (Claude)", configured=bool(settings.anthropic_api_key),
                default_model=settings.model, models=ANTHROPIC_MODELS,
            ),
            ProviderInfo(
                id="groq", label="Groq", configured=bool(settings.groq_api_key),
                default_model=settings.groq_model, models=GROQ_MODELS,
            ),
        ],
    )


@router.post("/test", response_model=TestResponse, response_model_by_alias=True)
def test(choice: LlmChoice, settings: Settings = Depends(get_settings)) -> TestResponse:
    """Faz uma chamada minima (resposta estruturada, como a analise de planilha usa) e mede o tempo."""
    llm = LlmClient(settings, choice)
    if not llm.enabled:
        return TestResponse(ok=False, provider=llm.provider, model=llm.model, latency_ms=None,
                            message="Chave deste provedor não configurada no servidor de IA.")
    started = time.perf_counter()
    try:
        llm.parse("Você é um verificador de conexão.", 'Responda com {"ok": true}.', _Ping)
    except LlmUnavailableError as exc:
        return TestResponse(ok=False, provider=llm.provider, model=llm.model, latency_ms=None, message=str(exc)[:500])
    elapsed = int((time.perf_counter() - started) * 1000)
    return TestResponse(ok=True, provider=llm.provider, model=llm.model, latency_ms=elapsed,
                        message="Conexão funcionando.")
