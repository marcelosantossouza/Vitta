"""Cliente LLM. Sem chave configurada, devolve respostas deterministicas (modo offline/dev)."""

from anthropic import Anthropic

from app.config import Settings


class LlmClient:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._client = Anthropic(api_key=settings.anthropic_api_key) if settings.anthropic_api_key else None

    @property
    def enabled(self) -> bool:
        return self._client is not None

    def complete(self, system: str, prompt: str) -> str:
        if self._client is None:
            raise RuntimeError("LLM desabilitado: defina VITTA_AI_ANTHROPIC_API_KEY.")
        message = self._client.messages.create(
            model=self._settings.model,
            max_tokens=self._settings.max_tokens,
            system=system,
            messages=[{"role": "user", "content": prompt}],
        )
        return "".join(block.text for block in message.content if block.type == "text")
