import json

import httpx
from fastapi.testclient import TestClient
from pydantic import BaseModel

from app.config import Settings, get_settings
from app.llm import LlmChoice, LlmClient, LlmUnavailableError, _AnthropicBackend, _GroqBackend
from app.main import app

client = TestClient(app)


class _Out(BaseModel):
    ok: bool


def test_choice_selects_provider_and_model() -> None:
    settings = Settings(anthropic_api_key="a", groq_api_key="g")
    llm = LlmClient(settings, LlmChoice(provider="groq", model="llama-3.3-70b-versatile", effort="low"))
    assert (llm.provider, llm.model, llm.effort, llm.enabled) == ("groq", "llama-3.3-70b-versatile", "low", True)
    assert isinstance(llm._backend, _GroqBackend)


def test_without_choice_uses_server_defaults() -> None:
    llm = LlmClient(Settings(anthropic_api_key="a", model="claude-haiku-5-5", effort="medium"))
    assert (llm.provider, llm.model) == ("anthropic", "claude-haiku-5-5")
    assert isinstance(llm._backend, _AnthropicBackend)


def test_missing_key_for_chosen_provider_disables() -> None:
    llm = LlmClient(Settings(anthropic_api_key="a", groq_api_key=None), LlmChoice(provider="groq"))
    assert not llm.enabled


def _groq_with(handler) -> _GroqBackend:
    backend = _GroqBackend("g", "openai/gpt-oss-20b", "low", Settings())
    backend._http = httpx.Client(base_url="https://groq.test", transport=httpx.MockTransport(handler))
    return backend


def test_groq_parse_validates_json_and_sends_json_mode() -> None:
    seen = {}

    def handler(request: httpx.Request) -> httpx.Response:
        seen.update(json.loads(request.content))
        return httpx.Response(200, json={"choices": [{"message": {"content": '{"ok": true}'}}]})

    assert _groq_with(handler).parse("sys", "prompt", _Out).ok is True
    assert seen["response_format"] == {"type": "json_object"}
    assert seen["reasoning_effort"] == "low"
    assert "JSON Schema" in seen["messages"][0]["content"]


def test_groq_bad_json_or_error_raises_unavailable() -> None:
    bad = _groq_with(lambda r: httpx.Response(200, json={"choices": [{"message": {"content": '{"nope": 1}'}}]}))
    error = _groq_with(lambda r: httpx.Response(401, json={"error": {"message": "Invalid API Key"}}))
    for backend in (bad, error):
        try:
            backend.parse("sys", "prompt", _Out)
            raise AssertionError("deveria falhar")
        except LlmUnavailableError as exc:
            assert "Groq" in str(exc)


def test_providers_reports_configured_flags() -> None:
    app.dependency_overrides[get_settings] = lambda: Settings(anthropic_api_key=None, groq_api_key="g")
    try:
        body = client.get("/v1/llm/providers").json()
    finally:
        app.dependency_overrides.clear()
    flags = {p["id"]: p["configured"] for p in body["providers"]}
    assert flags == {"anthropic": False, "groq": True}
    assert body["providers"][0]["models"][0]["id"] == "claude-haiku-5-5"


def test_test_endpoint_without_key_is_not_ok() -> None:
    app.dependency_overrides[get_settings] = lambda: Settings(anthropic_api_key=None, groq_api_key=None)
    try:
        body = client.post("/v1/llm/test", json={"provider": "anthropic", "model": "claude-haiku-5-5"}).json()
    finally:
        app.dependency_overrides.clear()
    assert body["ok"] is False and body["latencyMs"] is None
