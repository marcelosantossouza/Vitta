from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_prefix="VITTA_AI_")

    # Chaves ficam SO aqui (servidor). A tela de configuracao da Vitta escolhe provedor/modelo, nunca ve a chave.
    anthropic_api_key: str | None = None
    groq_api_key: str | None = None

    # Padroes usados quando a Vitta.API nao manda escolha (ninguem salvou a tela de configuracao ainda).
    provider: str = "anthropic"
    model: str = "claude-opus-5-5"  # modelo padrao da Anthropic
    groq_model: str = "openai/gpt-oss-120b"
    # Esforco de raciocinio: entender um layout de planilha nao exige "high".
    effort: str = "medium"
    max_tokens: int = 16000
    groq_base_url: str = "https://api.groq.com/openai/v1"
    timeout_seconds: float = 90


@lru_cache
def get_settings() -> Settings:
    return Settings()
