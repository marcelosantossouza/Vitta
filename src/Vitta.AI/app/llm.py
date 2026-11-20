"""Cliente LLM (Anthropic). Sem chave configurada o servico fica em modo offline e os endpoints de IA
respondem 503, para que a Vitta.API use sua heuristica local."""

from typing import TypeVar

import anthropic
from pydantic import BaseModel

from app.config import Settings

T = TypeVar("T", bound=BaseModel)

# Se o modelo principal recusar (falso positivo dos filtros de seguranca), a API refaz no modelo reserva.
_FALLBACK_BETA = "server-side-fallback-2026-07-01"


class LlmUnavailableError(RuntimeError):
    """LLM desligado (sem chave), indisponivel ou recusou a tarefa."""


class LlmClient:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._client = anthropic.Anthropic(api_key=settings.anthropic_api_key) if settings.anthropic_api_key else None

    @property
    def enabled(self) -> bool:
        return self._client is not None

    def _require(self) -> anthropic.Anthropic:
        if self._client is None:
            raise LlmUnavailableError("LLM desabilitado: defina VITTA_AI_ANTHROPIC_API_KEY.")
        return self._client

    def complete(self, system: str, prompt: str) -> str:
        message = self._require().messages.create(
            model=self._settings.model,
            max_tokens=self._settings.max_tokens,
            output_config={"effort": self._settings.effort},
            system=system,
            messages=[{"role": "user", "content": prompt}],
        )
        return "".join(block.text for block in message.content if block.type == "text")

    def parse(self, system: str, prompt: str, output: type[T]) -> T:
        """Resposta estruturada: o SDK valida o JSON contra o modelo Pydantic."""
        try:
            response = self._require().beta.messages.parse(
                model=self._settings.model,
                max_tokens=self._settings.max_tokens,
                betas=[_FALLBACK_BETA],
                fallbacks="default",
                output_config={"effort": self._settings.effort},
                system=system,
                messages=[{"role": "user", "content": prompt}],
                output_format=output,
            )
        except anthropic.APIConnectionError as exc:
            raise LlmUnavailableError(f"Sem conexao com a Anthropic: {exc}") from exc
        except anthropic.APIStatusError as exc:
            raise LlmUnavailableError(f"Anthropic respondeu {exc.status_code}: {exc.message}") from exc

        if response.stop_reason == "refusal" or response.parsed_output is None:
            raise LlmUnavailableError(f"Sem resposta estruturada (stop_reason={response.stop_reason}).")
        return response.parsed_output
