from fastapi import FastAPI

from app.routers import portfolio

app = FastAPI(title="Vitta AI", version="0.1.0")
app.include_router(portfolio.router)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}
