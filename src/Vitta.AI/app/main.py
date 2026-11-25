from fastapi import FastAPI

from app.routers import llm, portfolio, spreadsheets

app = FastAPI(title="Vitta AI", version="0.1.0")
app.include_router(portfolio.router)
app.include_router(spreadsheets.router)
app.include_router(llm.router)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}
