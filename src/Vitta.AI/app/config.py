from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_prefix="VITTA_AI_")

    anthropic_api_key: str | None = None
    model: str = "claude-opus-5-5"
    # Esforco de raciocinio: entender um layout de planilha nao exige "high".
    effort: str = "medium"
    max_tokens: int = 16000


@lru_cache
def get_settings() -> Settings:
    return Settings()
