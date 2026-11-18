from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_health() -> None:
    assert client.get("/health").json() == {"status": "ok"}


def test_summarize_offline() -> None:
    response = client.post(
        "/v1/portfolio/summarize",
        json={"insurerName": "MAG", "referenceMonth": "2026-10", "changes": ["Saiu: Bruno", "Entrou: Carla"]},
    )
    assert response.status_code == 200
    assert "2 mudanca" in response.json()["summary"]
