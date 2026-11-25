"""Cliente LLM com dois provedores: Anthropic (Claude) e Groq (API compativel com OpenAI).

A Vitta.API manda em cada pedido qual provedor/modelo/esforco usar (tela de configuracao). Sem chave do provedor
escolhido o cliente fica desligado e os endpoints de IA respondem 503, para que a Vitta.API use sua heuristica local.
"""

import json
from typing import Literal, Protocol, TypeVar

import anthropic
import httpx
from pydantic import BaseModel, ConfigDict, ValidationError
from pydantic.alias_generators import to_camel

from app.config import Settings

T = TypeVar("T", bound=BaseModel)

ProviderId = Literal["anthropic", "groq"]
Effort = Literal["low", "medium", "high"]

# Se o modelo principal recusar (falso positivo dos filtros de seguranca), a API refaz no modelo reserva.
# So alguns modelos aceitam o parametro; o Haiku, por exemplo, devolve 400 se ele for enviado.
_FALLBACK_BETA = "server-side-fallback-2026-07-01"
_FALLBACK_MODELS = ("claude-opus-5-5", "claude-sonnet-5-5", "claude-fable-5-2", "claude-fable-5-1")


class LlmUnavailableError(RuntimeError):
    """LLM desligado (sem chave), indisponivel ou recusou a tarefa."""


class LlmChoice(BaseModel):
    """Escolha feita na tela de configuracao da Vitta. Campos vazios = padrao do servidor (.env)."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    provider: ProviderId | None = None
    model: str | None = None
    effort: Effort | None = None


class _Backend(Protocol):
    def complete(self, system: str, prompt: str) -> str: ...
    def parse(self, system: str, prompt: str, output: type[T]) -> T: ...


class _AnthropicBackend:
    def __init__(self, api_key: str, model: str, effort: str, settings: Settings) -> None:
        self._client = anthropic.Anthropic(api_key=api_key, timeout=settings.timeout_seconds)
        self._model = model
        self._effort = effort
        self._max_tokens = settings.max_tokens

    def complete(self, system: str, prompt: str) -> str:
        try:
            message = self._client.messages.create(
                model=self._model,
                max_tokens=self._max_tokens,
                output_config={"effort": self._effort},
                system=system,
                messages=[{"role": "user", "content": prompt}],
            )
        except anthropic.APIConnectionError as exc:
            raise LlmUnavailableError(f"Sem conexão com a Anthropic: {exc}") from exc
        except anthropic.APIStatusError as exc:
            raise LlmUnavailableError(f"Anthropic respondeu {exc.status_code}: {exc.message}") from exc
        return "".join(block.text for block in message.content if block.type == "text")

    def parse(self, system: str, prompt: str, output: type[T]) -> T:
        """Resposta estruturada: o SDK valida o JSON contra o modelo Pydantic."""
        extra: dict = {}
        if self._model.startswith(_FALLBACK_MODELS):
            extra = {"betas": [_FALLBACK_BETA], "fallbacks": "default"}
        try:
            response = self._client.beta.messages.parse(
                model=self._model,
                max_tokens=self._max_tokens,
                output_config={"effort": self._effort},
                system=system,
                messages=[{"role": "user", "content": prompt}],
                output_format=output,
                **extra,
            )
        except anthropic.APIConnectionError as exc:
            raise LlmUnavailableError(f"Sem conexão com a Anthropic: {exc}") from exc
        except anthropic.APIStatusError as exc:
            raise LlmUnavailableError(f"Anthropic respondeu {exc.status_code}: {exc.message}") from exc

        if response.stop_reason == "refusal" or response.parsed_output is None:
            raise LlmUnavailableError(f"Sem resposta estruturada (stop_reason={response.stop_reason}).")
        return response.parsed_output


class _GroqBackend:
    """Groq pela API compativel com OpenAI (chat/completions)."""

    def __init__(self, api_key: str, model: str, effort: str, settings: Settings) -> None:
        self._http = httpx.Client(
            base_url=settings.groq_base_url,
            headers={"Authorization": f"Bearer {api_key}"},
            timeout=settings.timeout_seconds,
        )
        self._model = model
        self._effort = effort
        self._max_tokens = min(settings.max_tokens, 8192)

    def _chat(self, system: str, prompt: str, json_mode: bool) -> str:
        body: dict = {
            "model": self._model,
            "max_completion_tokens": self._max_tokens,
            "messages": [{"role": "system", "content": system}, {"role": "user", "content": prompt}],
        }
        if json_mode:
            body["response_format"] = {"type": "json_object"}
        if self._model.startswith("openai/gpt-oss"):
            # Modelos gpt-oss aceitam nivel de raciocinio; os demais ignorariam ou recusariam o campo.
            body["reasoning_effort"] = self._effort
        try:
            response = self._http.post("/chat/completions", json=body)
        except httpx.HTTPError as exc:
            raise LlmUnavailableError(f"Sem conexão com a Groq: {exc}") from exc
        if response.status_code != 200:
            raise LlmUnavailableError(f"Groq respondeu {response.status_code}: {_error_message(response)}")
        try:
            return response.json()["choices"][0]["message"]["content"] or ""
        except (ValueError, KeyError, IndexError, TypeError) as exc:
            raise LlmUnavailableError("Resposta inesperada da Groq.") from exc

    def complete(self, system: str, prompt: str) -> str:
        return self._chat(system, prompt, json_mode=False)

    def parse(self, system: str, prompt: str, output: type[T]) -> T:
        # Modo JSON + esquema no prompt; quem garante o formato e a validacao Pydantic abaixo.
        schema = json.dumps(output.model_json_schema(), ensure_ascii=False)
        system_json = (
            f"{system}\n\nResponda APENAS com um objeto JSON valido (sem texto antes ou depois) "
            f"que siga exatamente este JSON Schema:\n{schema}"
        )
        text = self._chat(system_json, prompt, json_mode=True)
        try:
            return output.model_validate_json(text)
        except ValidationError as exc:
            raise LlmUnavailableError(f"A Groq devolveu JSON fora do formato esperado: {exc.error_count()} erro(s).") from exc


def _error_message(response: httpx.Response) -> str:
    try:
        return response.json()["error"]["message"]
    except (ValueError, KeyError, TypeError):
        return response.text[:200]


class LlmClient:
    def __init__(self, settings: Settings, choice: LlmChoice | None = None) -> None:
        choice = choice or LlmChoice()
        self.provider: ProviderId = choice.provider or ("groq" if settings.provider == "groq" else "anthropic")
        default_model = settings.groq_model if self.provider == "groq" else settings.model
        self.model = (choice.model or "").strip() or default_model
        self.effort = choice.effort or settings.effort

        key = settings.groq_api_key if self.provider == "groq" else settings.anthropic_api_key
        backend_type = _GroqBackend if self.provider == "groq" else _AnthropicBackend
        self._backend: _Backend | None = backend_type(key, self.model, self.effort, settings) if key else None

    @property
    def enabled(self) -> bool:
        return self._backend is not None

    def _require(self) -> _Backend:
        if self._backend is None:
            env = "VITTA_AI_GROQ_API_KEY" if self.provider == "groq" else "VITTA_AI_ANTHROPIC_API_KEY"
            raise LlmUnavailableError(f"LLM desabilitado: defina {env} no .env do serviço de IA.")
        return self._backend

    def complete(self, system: str, prompt: str) -> str:
        return self._require().complete(system, prompt)

    def parse(self, system: str, prompt: str, output: type[T]) -> T:
        return self._require().parse(system, prompt, output)
