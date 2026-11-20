from fastapi.testclient import TestClient

from app.config import Settings, get_settings
from app.llm import LlmClient
from app.main import app
from app.routers import spreadsheets

client = TestClient(app)

REQUEST = {
    "kind": "Portfolio",
    "fileName": "carteira-mag.xlsx",
    "sheetName": "Carteira",
    "rows": [
        ["Relatório de carteira", "", ""],
        ["Nº Apólice", "Segurado", "CPF"],
        ["999999", "Ana Lima", "999.999.999-99"],
    ],
    "fields": [
        {"field": "policyNumber", "label": "Nº da apólice", "required": True, "multiple": False, "description": "x"},
        {"field": "holderName", "label": "Segurado", "required": True, "multiple": False, "description": "x"},
        {"field": "holderCpf", "label": "CPF", "required": False, "multiple": False, "description": "x"},
    ],
}


def test_offline_returns_503() -> None:
    app.dependency_overrides[get_settings] = lambda: Settings(anthropic_api_key=None)
    try:
        assert client.post("/v1/spreadsheets/analyze", json=REQUEST).status_code == 503
    finally:
        app.dependency_overrides.clear()


def test_ai_answer_is_filtered(monkeypatch) -> None:
    captured = {}

    def fake_parse(self, system, prompt, output):
        captured["prompt"] = prompt
        return output(
            header_row=2,
            columns=[
                {"field": "policyNumber", "column_number": 1},
                {"field": "holderName", "column_number": 2},
                {"field": "inventado", "column_number": 3},
                {"field": "holderCpf", "column_number": 9},
            ],
            suggested_name="Carteira MAG",
            notes="Confira o CPF.",
            confidence="high",
        )

    monkeypatch.setattr(LlmClient, "enabled", property(lambda self: True))
    monkeypatch.setattr(LlmClient, "parse", fake_parse)

    body = client.post("/v1/spreadsheets/analyze", json=REQUEST).json()

    assert body["headerRow"] == 2
    assert body["columns"] == [{"field": "policyNumber", "columnNumber": 1}, {"field": "holderName", "columnNumber": 2}]
    assert body["suggestedName"] == "Carteira MAG"
    assert "Linha 2:" in captured["prompt"] and "Nº Apólice" in captured["prompt"]


def test_prompt_mentions_kind() -> None:
    req = spreadsheets.AnalyzeRequest.model_validate({**REQUEST, "kind": "Prospects"})
    assert "POSSIVEIS CLIENTES" in spreadsheets.build_prompt(req)


def test_pasted_table_without_header_keeps_row_zero(monkeypatch) -> None:
    def fake_parse(self, system, prompt, output):
        assert "header_row = 0" in system
        return output(header_row=0, columns=[{"field": "holderCpf", "column_number": 2}],
                      suggested_name="Carteira MAG (site)", notes="Sem nº de apólice: comparação pelo CPF.", confidence="medium")

    monkeypatch.setattr(LlmClient, "enabled", property(lambda self: True))
    monkeypatch.setattr(LlmClient, "parse", fake_parse)

    pasted = {**REQUEST, "fileName": "texto-colado.txt", "sheetName": None,
              "rows": [["9999999", "999.999.999-99", "ANA LIMA"], ["9999999", "999.999.999-99", "JOSE SOUZA"]]}
    body = client.post("/v1/spreadsheets/analyze", json=pasted).json()

    assert body["headerRow"] == 0
    assert body["columns"] == [{"field": "holderCpf", "columnNumber": 2}]
